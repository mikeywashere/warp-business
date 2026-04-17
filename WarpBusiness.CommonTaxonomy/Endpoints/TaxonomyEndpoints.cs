using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.CommonTaxonomy.Data;
using WarpBusiness.CommonTaxonomy.Models;
using WarpBusiness.CommonTaxonomy.Services;

namespace WarpBusiness.CommonTaxonomy.Endpoints;

public static class TaxonomyEndpoints
{
    public static void MapTaxonomyApiEndpoints(this WebApplication app)
    {
        var root = app.MapGroup("/api/taxonomy").RequireAuthorization();

        root.MapGet("providers", GetProviders).WithName("GetTaxonomyProviders");

        var providers = root.MapGroup("providers/{key}");
        providers.MapPost("download", DownloadProvider)
            .RequireAuthorization(new AuthorizeAttribute { Roles = "admin" })
            .WithName("DownloadTaxonomyProvider");
        providers.MapGet("nodes", GetProviderNodes).WithName("GetTaxonomyProviderNodes");
        providers.MapGet("nodes/stream", StreamProviderNodes).WithName("StreamTaxonomyProviderNodes");
        providers.MapGet("nodes/search", SearchProviderNodes).WithName("SearchTaxonomyProviderNodes");
        providers.MapPost("import", ImportProviderFile)
            .RequireAuthorization(new AuthorizeAttribute { Roles = "admin" })
            .WithName("ImportTaxonomyProviderFile");

        root.MapGet("nodes/{id:guid}", GetNode).WithName("GetTaxonomyNode");
        root.MapGet("nodes/{id:guid}/children", GetNodeChildren).WithName("GetTaxonomyNodeChildren");
    }

    private static async Task<IResult> GetProviders(
        CommonTaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var providers = await db.Providers
            .OrderBy(p => p.Key)
            .Select(p => new
            {
                p.Id,
                p.Key,
                p.DisplayName,
                p.SupportsApiDownload,
                p.SupportsFileImport,
                p.LastDownloadedAt,
                p.LastDownloadChecksum,
                p.IsActive,
                NodeCount = db.Nodes.Count(n => n.ProviderId == p.Id)
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(providers);
    }

    private static async Task<IResult> DownloadProvider(
        string key,
        CommonTaxonomyDbContext db,
        IEnumerable<ITaxonomyDownloader> downloaders,
        CancellationToken cancellationToken)
    {
        var downloader = downloaders.FirstOrDefault(d => d.ProviderKey == key);
        if (downloader is null)
            return Results.NotFound(new { message = "Provider downloader not found." });

        var result = await downloader.DownloadAsync(cancellationToken);
        if (result.Success && !result.WasSkipped)
        {
            var provider = await db.Providers.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);
            if (provider is not null)
            {
                provider.LastDownloadedAt = DateTimeOffset.UtcNow;
                provider.LastDownloadChecksum = result.Checksum;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> StreamProviderNodes(
        string key,
        CommonTaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);
        if (provider is null)
            return Results.NotFound(new { message = "Provider not found." });

        var providerId = provider.Id;
        var stream = db.Nodes
            .Where(n => n.ProviderId == providerId)
            .OrderBy(n => n.Depth).ThenBy(n => n.Name)
            .Include(n => n.Attributes)
            .AsAsyncEnumerable()
            .Select(MapNodeResponse);

        return Results.Ok(stream);
    }

    private static async Task<IResult> GetProviderNodes(
        string key,
        [FromQuery] Guid? parentId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CommonTaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);

        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);
        if (provider is null)
            return Results.NotFound(new { message = "Provider not found." });

        var query = db.Nodes
            .Where(n => n.ProviderId == provider.Id && n.ParentId == parentId)
            .OrderBy(n => n.Name)
            .Include(n => n.Attributes);

        var total = await query.CountAsync(cancellationToken);
        var nodes = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            page,
            pageSize,
            totalCount = total,
            nodes = nodes.Select(MapNodeResponse).ToList()
        });
    }

    private static async Task<IResult> SearchProviderNodes(
        string key,
        [FromQuery] string q,
        CommonTaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.BadRequest(new { message = "Query parameter q is required." });

        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);
        if (provider is null)
            return Results.NotFound(new { message = "Provider not found." });

        var nodes = await db.Nodes
            .Where(n => n.ProviderId == provider.Id
                        && (EF.Functions.ILike(n.Name, $"%{q}%")
                            || EF.Functions.ILike(n.FullPath, $"%{q}%")))
            .OrderBy(n => n.FullPath)
            .Include(n => n.Attributes)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Results.Ok(nodes.Select(MapNodeResponse).ToList());
    }

    private static async Task<IResult> GetNode(
        Guid id,
        CommonTaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var node = await db.Nodes
            .Include(n => n.Attributes)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        return node is null ? Results.NotFound() : Results.Ok(MapNodeResponse(node));
    }

    private static async Task<IResult> GetNodeChildren(
        Guid id,
        CommonTaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var children = await db.Nodes
            .Where(n => n.ParentId == id)
            .OrderBy(n => n.Name)
            .Include(n => n.Attributes)
            .ToListAsync(cancellationToken);

        return Results.Ok(children.Select(MapNodeResponse).ToList());
    }

    private static async Task<IResult> ImportProviderFile(
        string key,
        [FromForm] IFormFile file,
        CommonTaxonomyDbContext db,
        IEnumerable<IFileTaxonomyDownloader> downloaders,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { message = "File upload is required." });

        var downloader = downloaders.FirstOrDefault(d => d.ProviderKey == key);
        if (downloader is null)
            return Results.NotFound(new { message = "Provider does not support file import." });

        await using var stream = file.OpenReadStream();
        var result = await downloader.ImportFromFileAsync(stream, file.FileName, cancellationToken);
        if (result.Success && !result.WasSkipped)
        {
            var provider = await db.Providers.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);
            if (provider is not null)
            {
                provider.LastDownloadedAt = DateTimeOffset.UtcNow;
                provider.LastDownloadChecksum = result.Checksum;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return Results.Ok(result);
    }

    private static object MapNodeResponse(TaxonomyNode node)
    {
        return new
        {
            node.Id,
            node.ExternalId,
            node.FullPath,
            node.Name,
            node.ParentId,
            node.Depth,
            node.IsLeaf,
            Attributes = node.Attributes.Select(a => new
            {
                a.Id,
                a.Name,
                ValueType = a.ValueType.ToString(),
                a.IsRequired,
                a.AllowedValues,
                a.Unit,
                a.IsInherited
            }).ToList()
        };
    }
}
