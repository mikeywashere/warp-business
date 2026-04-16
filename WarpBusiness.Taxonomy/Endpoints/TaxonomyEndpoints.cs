using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using WarpBusiness.Taxonomy.Data;
using WarpBusiness.Taxonomy.Models;
using WarpBusiness.Taxonomy.Services;

namespace WarpBusiness.Taxonomy.Endpoints;

public static class TaxonomyEndpoints
{
    private static readonly Regex SlugCleanup = new(@"[^a-z0-9\-]+", RegexOptions.Compiled);
    private static readonly Regex SlugHyphens = new(@"-{2,}", RegexOptions.Compiled);

    public static void MapTaxonomyEndpoints(this WebApplication app)
    {
        var taxonomy = app.MapGroup("/api/taxonomy").RequireAuthorization();
        taxonomy.MapGet("", GetTaxonomyTree).WithName("GetTaxonomyTree");
        taxonomy.MapGet("{id:guid}", GetTaxonomyNode).WithName("GetTaxonomyNode");
        taxonomy.MapPost("", CreateTaxonomyNode).WithName("CreateTaxonomyNode");
        taxonomy.MapPut("{id:guid}", UpdateTaxonomyNode).WithName("UpdateTaxonomyNode");
        taxonomy.MapDelete("{id:guid}", DeleteTaxonomyNode).WithName("DeleteTaxonomyNode");
        taxonomy.MapPost("{id:guid}/move", MoveTaxonomyNode).WithName("MoveTaxonomyNode");

        var external = app.MapGroup("/api/taxonomy/external").RequireAuthorization();
        external.MapGet("providers", GetProviderStatus).WithName("GetTaxonomyProviderStatus");
        external.MapPost("{provider}/download", TriggerDownload).WithName("TriggerTaxonomyDownload");
        external.MapGet("{provider}/tree", GetExternalTree).WithName("GetExternalTaxonomyTree");
        external.MapGet("{provider}/search", SearchExternalNodes).WithName("SearchExternalTaxonomyNodes");

        var import = app.MapGroup("/api/taxonomy/import").RequireAuthorization();
        import.MapPost("", ImportNodes).WithName("ImportTaxonomyNodes");
        import.MapGet("preview", PreviewImport).WithName("PreviewTaxonomyImport");
    }

    private static async Task<IResult> GetTaxonomyTree(
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var nodes = await db.TaxonomyNodes
            .Where(n => n.TenantId == tenantId.Value)
            .OrderBy(n => n.MaterializedPath)
            .Select(n => new TaxonomyNodeResponse(
                n.Id,
                n.TenantId,
                n.ParentNodeId,
                n.Name,
                n.Description,
                n.Level,
                n.MaterializedPath,
                n.IsActive,
                n.SourceProvider,
                n.SourceExternalId,
                n.SourceImportedAt,
                db.TaxonomyNodes.Count(c => c.ParentNodeId == n.Id && c.TenantId == tenantId.Value),
                n.CreatedAt,
                n.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(nodes);
    }

    private static async Task<IResult> GetTaxonomyNode(
        Guid id,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var node = await db.TaxonomyNodes
            .Where(n => n.Id == id && n.TenantId == tenantId.Value)
            .Select(n => new TaxonomyNodeResponse(
                n.Id,
                n.TenantId,
                n.ParentNodeId,
                n.Name,
                n.Description,
                n.Level,
                n.MaterializedPath,
                n.IsActive,
                n.SourceProvider,
                n.SourceExternalId,
                n.SourceImportedAt,
                db.TaxonomyNodes.Count(c => c.ParentNodeId == n.Id && c.TenantId == tenantId.Value),
                n.CreatedAt,
                n.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return node is null ? Results.NotFound() : Results.Ok(node);
    }

    private static async Task<IResult> CreateTaxonomyNode(
        [FromBody] CreateTaxonomyNodeRequest request,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Taxonomy node name is required." });

        TaxonomyNode? parent = null;
        if (request.ParentNodeId.HasValue)
        {
            parent = await db.TaxonomyNodes
                .FirstOrDefaultAsync(
                    n => n.Id == request.ParentNodeId.Value && n.TenantId == tenantId.Value,
                    cancellationToken);
            if (parent is null)
                return Results.BadRequest(new { message = "Parent taxonomy node not found in this tenant." });
        }

        var level = parent?.Level + 1 ?? 0;
        var materializedPath = BuildMaterializedPath(parent?.MaterializedPath, Slugify(request.Name));
        var sortOrder = await db.TaxonomyNodes.CountAsync(
            n => n.TenantId == tenantId.Value && n.ParentNodeId == request.ParentNodeId,
            cancellationToken);

        var node = new TaxonomyNode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ParentNodeId = request.ParentNodeId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Level = level,
            MaterializedPath = materializedPath,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.TaxonomyNodes.Add(node);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A taxonomy node with this name already exists at this level." });
        }

        return Results.Created(
            $"/api/taxonomy/{node.Id}",
            MapResponse(node, 0));
    }

    private static async Task<IResult> UpdateTaxonomyNode(
        Guid id,
        [FromBody] UpdateTaxonomyNodeRequest request,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var node = await db.TaxonomyNodes
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId.Value, cancellationToken);
        if (node is null)
            return Results.NotFound();

        var nameChanged = false;
        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Taxonomy node name is required." });

            var trimmed = request.Name.Trim();
            if (!string.Equals(trimmed, node.Name, StringComparison.Ordinal))
            {
                node.Name = trimmed;
                nameChanged = true;
            }
        }

        if (request.Description is not null)
            node.Description = request.Description;

        if (request.IsActive.HasValue)
            node.IsActive = request.IsActive.Value;

        if (nameChanged)
        {
            string? parentPath = null;
            int parentLevel = -1;
            if (node.ParentNodeId.HasValue)
            {
                var parent = await db.TaxonomyNodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        n => n.Id == node.ParentNodeId.Value && n.TenantId == tenantId.Value,
                        cancellationToken);
                parentPath = parent?.MaterializedPath;
                parentLevel = parent?.Level ?? -1;
            }

            var newLevel = parentLevel + 1;
            var newPath = BuildMaterializedPath(parentPath, Slugify(node.Name));
            await UpdateSubtreePathsAsync(db, node, newPath, newLevel, cancellationToken);
        }
        else
        {
            node.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A taxonomy node with this name already exists at this level." });
        }

        var childCount = await db.TaxonomyNodes.CountAsync(
            n => n.ParentNodeId == node.Id && n.TenantId == tenantId.Value,
            cancellationToken);

        return Results.Ok(MapResponse(node, childCount));
    }

    private static async Task<IResult> DeleteTaxonomyNode(
        Guid id,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var node = await db.TaxonomyNodes
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId.Value, cancellationToken);
        if (node is null)
            return Results.NotFound();

        var hasChildren = await db.TaxonomyNodes.AnyAsync(
            n => n.ParentNodeId == id && n.TenantId == tenantId.Value,
            cancellationToken);
        if (hasChildren)
            return Results.Conflict(new { message = "Cannot delete a taxonomy node that has children." });

        db.TaxonomyNodes.Remove(node);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> MoveTaxonomyNode(
        Guid id,
        [FromBody] MoveTaxonomyNodeRequest request,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var node = await db.TaxonomyNodes
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId.Value, cancellationToken);
        if (node is null)
            return Results.NotFound();

        if (request.NewParentId == node.Id)
            return Results.BadRequest(new { message = "A taxonomy node cannot be its own parent." });

        TaxonomyNode? newParent = null;
        if (request.NewParentId.HasValue)
        {
            newParent = await db.TaxonomyNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.Id == request.NewParentId.Value && n.TenantId == tenantId.Value,
                    cancellationToken);
            if (newParent is null)
                return Results.BadRequest(new { message = "New parent taxonomy node not found in this tenant." });

            if (newParent.MaterializedPath.StartsWith(node.MaterializedPath + "/", StringComparison.Ordinal))
                return Results.BadRequest(new { message = "Cannot move a taxonomy node under its own descendant." });
        }

        var newLevel = newParent?.Level + 1 ?? 0;
        var newPath = BuildMaterializedPath(newParent?.MaterializedPath, Slugify(node.Name));

        node.ParentNodeId = request.NewParentId;
        await UpdateSubtreePathsAsync(db, node, newPath, newLevel, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A taxonomy node with this name already exists at this level." });
        }

        var childCount = await db.TaxonomyNodes.CountAsync(
            n => n.ParentNodeId == node.Id && n.TenantId == tenantId.Value,
            cancellationToken);

        return Results.Ok(MapResponse(node, childCount));
    }

    private static async Task<IResult> GetProviderStatus(
        TaxonomyDbContext db,
        IEnumerable<ITaxonomyDownloader> downloaders,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var latestCaches = await db.ExternalTaxonomyCaches
            .AsNoTracking()
            .GroupBy(c => c.Provider)
            .Select(g => g.OrderByDescending(c => c.DownloadedAt).FirstOrDefault())
            .ToListAsync(cancellationToken);

        var cacheLookup = latestCaches
            .Where(c => c is not null)
            .ToDictionary(c => c!.Provider);

        var downloaderLookup = downloaders.ToDictionary(d => d.Provider);

        var responses = Enum.GetValues<TaxonomyProvider>()
            .Select(provider =>
            {
                cacheLookup.TryGetValue(provider, out var cache);
                downloaderLookup.TryGetValue(provider, out var downloader);
                var enabled = provider switch
                {
                    TaxonomyProvider.Google => true,
                    TaxonomyProvider.Amazon => IsAmazonConfigured(configuration),
                    TaxonomyProvider.Ebay => IsEbayConfigured(configuration),
                    TaxonomyProvider.Etsy => IsEtsyConfigured(configuration),
                    _ => false
                };

                return new ProviderStatusResponse(
                    provider,
                    enabled,
                    downloader?.RequiresApiKey ?? true,
                    cache?.DownloadedAt,
                    cache?.NodeCount ?? 0,
                    cache?.SourceVersion,
                    cache?.Status);
            })
            .ToList();

        return Results.Ok(responses);
    }

    private static async Task<IResult> TriggerDownload(
        TaxonomyProvider provider,
        TaxonomyDownloadService downloadService,
        CancellationToken cancellationToken)
    {
        var cache = await downloadService.DownloadAsync(provider, cancellationToken);
        return Results.Ok(cache);
    }

    private static async Task<IResult> GetExternalTree(
        TaxonomyProvider provider,
        [FromQuery] string? parentExternalId,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var importedIds = await db.TaxonomyNodes
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId.Value
                && n.SourceProvider == provider
                && n.SourceExternalId != null)
            .Select(n => n.SourceExternalId!)
            .ToListAsync(cancellationToken);

        var importedLookup = importedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var query = db.ExternalTaxonomyNodes
            .AsNoTracking()
            .Where(n => n.Provider == provider);

        if (!string.IsNullOrWhiteSpace(parentExternalId))
            query = query.Where(n => n.ParentExternalId == parentExternalId);

        var nodes = await query
            .OrderBy(n => n.FullPath)
            .Select(n => new ExternalNodeResponse(
                n.ExternalId,
                n.ParentExternalId,
                n.Name,
                n.FullPath,
                n.Level,
                n.IsLeaf,
                false))
            .ToListAsync(cancellationToken);

        var response = nodes
            .Select(n => n with { IsImported = importedLookup.Contains(n.ExternalId) })
            .ToList();

        return Results.Ok(response);
    }

    private static async Task<IResult> SearchExternalNodes(
        TaxonomyProvider provider,
        [FromQuery] string? q,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(q))
            return Results.BadRequest(new { message = "Search query is required." });

        var importedIds = await db.TaxonomyNodes
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId.Value
                && n.SourceProvider == provider
                && n.SourceExternalId != null)
            .Select(n => n.SourceExternalId!)
            .ToListAsync(cancellationToken);

        var importedLookup = importedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nodes = await db.ExternalTaxonomyNodes
            .AsNoTracking()
            .Where(n => n.Provider == provider
                && (EF.Functions.ILike(n.Name, $"%{q}%")
                    || EF.Functions.ILike(n.FullPath, $"%{q}%")))
            .OrderBy(n => n.FullPath)
            .Select(n => new ExternalNodeResponse(
                n.ExternalId,
                n.ParentExternalId,
                n.Name,
                n.FullPath,
                n.Level,
                n.IsLeaf,
                false))
            .ToListAsync(cancellationToken);

        var response = nodes
            .Select(n => n with { IsImported = importedLookup.Contains(n.ExternalId) })
            .ToList();

        return Results.Ok(response);
    }

    private static async Task<IResult> ImportNodes(
        [FromBody] ImportNodesRequest request,
        HttpContext httpContext,
        TaxonomyImportService importService,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (request.ExternalIds is null || request.ExternalIds.Count == 0)
            return Results.BadRequest(new { message = "External node IDs are required." });

        var result = await importService.ImportAsync(
            request.Provider,
            request.ExternalIds,
            tenantId.Value,
            request.TargetParentId,
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> PreviewImport(
        [FromBody] ImportNodesRequest request,
        HttpContext httpContext,
        TaxonomyImportService importService,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (request.ExternalIds is null || request.ExternalIds.Count == 0)
            return Results.BadRequest(new { message = "External node IDs are required." });

        var preview = await importService.PreviewImportAsync(
            request.Provider,
            request.ExternalIds,
            tenantId.Value,
            request.TargetParentId,
            cancellationToken);

        return Results.Ok(preview);
    }

    private static async Task UpdateSubtreePathsAsync(
        TaxonomyDbContext db,
        TaxonomyNode node,
        string newPath,
        int newLevel,
        CancellationToken cancellationToken)
    {
        var oldPath = node.MaterializedPath;
        var levelDelta = newLevel - node.Level;

        node.MaterializedPath = newPath;
        node.Level = newLevel;
        node.UpdatedAt = DateTime.UtcNow;

        if (string.Equals(oldPath, newPath, StringComparison.Ordinal) && levelDelta == 0)
            return;

        var descendants = await db.TaxonomyNodes
            .Where(n => n.TenantId == node.TenantId
                && n.Id != node.Id
                && n.MaterializedPath.StartsWith(oldPath + "/"))
            .ToListAsync(cancellationToken);

        foreach (var descendant in descendants)
        {
            descendant.MaterializedPath = newPath + descendant.MaterializedPath[oldPath.Length..];
            descendant.Level += levelDelta;
            descendant.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static TaxonomyNodeResponse MapResponse(TaxonomyNode node, int childCount)
        => new(
            node.Id,
            node.TenantId,
            node.ParentNodeId,
            node.Name,
            node.Description,
            node.Level,
            node.MaterializedPath,
            node.IsActive,
            node.SourceProvider,
            node.SourceExternalId,
            node.SourceImportedAt,
            childCount,
            node.CreatedAt,
            node.UpdatedAt);

    private static Guid? GetTenantId(HttpContext httpContext)
        => httpContext.Items["TenantId"] as Guid?;

    private static string BuildMaterializedPath(string? parentPath, string slug)
        => string.IsNullOrWhiteSpace(parentPath) ? $"/{slug}" : $"{parentPath}/{slug}";

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "node";

        var builder = new StringBuilder();
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else
                builder.Append('-');
        }

        var slug = SlugCleanup.Replace(builder.ToString(), "-");
        slug = SlugHyphens.Replace(slug, "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "node" : slug;
    }

    private static bool IsAmazonConfigured(IConfiguration configuration)
    {
        var accessKey = configuration["Taxonomy:Amazon:AccessKeyId"];
        var secretKey = configuration["Taxonomy:Amazon:SecretAccessKey"];
        var associateTag = configuration["Taxonomy:Amazon:AssociateTag"];
        return !string.IsNullOrWhiteSpace(accessKey)
            && !string.IsNullOrWhiteSpace(secretKey)
            && !string.IsNullOrWhiteSpace(associateTag);
    }

    private static bool IsEbayConfigured(IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("Taxonomy:Ebay:Enabled");
        var clientId = configuration["Taxonomy:Ebay:ClientId"];
        var clientSecret = configuration["Taxonomy:Ebay:ClientSecret"];
        return enabled && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
    }

    private static bool IsEtsyConfigured(IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("Taxonomy:Etsy:Enabled");
        var apiKey = configuration["Taxonomy:Etsy:ApiKey"];
        return enabled && !string.IsNullOrWhiteSpace(apiKey);
    }
}
