using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;
using WarpBusiness.CommonTaxonomy.Data;

namespace WarpBusiness.Api.Endpoints;

public static class CatalogTaxonomyEndpoints
{
    public static void MapCatalogTaxonomyEndpoints(this WebApplication app)
    {
        var mappings = app.MapGroup("/api/catalog/products/{productId:guid}/taxonomy-mappings").RequireAuthorization();
        mappings.MapGet("", GetMappings).WithName("GetCatalogTaxonomyMappings");
        mappings.MapPost("", CreateMapping).WithName("CreateCatalogTaxonomyMapping");
        mappings.MapDelete("{mappingId:guid}", DeleteMapping).WithName("DeleteCatalogTaxonomyMapping");
        mappings.MapGet("{mappingId:guid}/attributes", GetMappingAttributes).WithName("GetCatalogTaxonomyMappingAttributes");
        mappings.MapPut("{mappingId:guid}/attributes", UpsertMappingAttributes).WithName("UpsertCatalogTaxonomyMappingAttributes");
        mappings.MapGet("suggest-options", SuggestOptions).WithName("SuggestCatalogTaxonomyOptions");
    }

    private static async Task<IResult> GetMappings(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var mappings = await db.ProductTaxonomyMappings
            .Where(m => m.ProductId == productId && m.TenantId == tenantId.Value)
            .OrderBy(m => m.ProviderKey)
            .ThenBy(m => m.NodeFullPath)
            .ToListAsync(cancellationToken);

        return Results.Ok(mappings.Select(m => new
        {
            m.Id,
            m.ProviderKey,
            m.TaxonomyNodeId,
            m.NodeName,
            m.NodeFullPath,
            m.CreatedAt
        }).ToList());
    }

    private static async Task<IResult> CreateMapping(
        Guid productId,
        [FromBody] CreateProductTaxonomyMappingRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CommonTaxonomyDbContext taxonomyDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productExists = await db.Products.AnyAsync(
            p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (!productExists)
            return Results.NotFound();

        var provider = await taxonomyDb.Providers.FirstOrDefaultAsync(p => p.Key == request.ProviderKey, cancellationToken);
        if (provider is null)
            return Results.BadRequest(new { message = "Provider not found." });

        var node = await taxonomyDb.Nodes
            .FirstOrDefaultAsync(n => n.Id == request.TaxonomyNodeId && n.ProviderId == provider.Id, cancellationToken);
        if (node is null)
            return Results.BadRequest(new { message = "Taxonomy node not found for provider." });

        var mapping = new ProductTaxonomyMapping
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            TenantId = tenantId.Value,
            ProviderKey = request.ProviderKey,
            TaxonomyNodeId = request.TaxonomyNodeId,
            NodeName = string.IsNullOrWhiteSpace(request.NodeName) ? node.Name : request.NodeName,
            NodeFullPath = string.IsNullOrWhiteSpace(request.NodeFullPath) ? node.FullPath : request.NodeFullPath,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.ProductTaxonomyMappings.Add(mapping);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "Mapping already exists for this product and node." });
        }

        return Results.Created(
            $"/api/catalog/products/{productId}/taxonomy-mappings/{mapping.Id}",
            new
            {
                mapping.Id,
                mapping.ProviderKey,
                mapping.TaxonomyNodeId,
                mapping.NodeName,
                mapping.NodeFullPath,
                mapping.CreatedAt
            });
    }

    private static async Task<IResult> DeleteMapping(
        Guid productId,
        Guid mappingId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var mapping = await db.ProductTaxonomyMappings
            .FirstOrDefaultAsync(m => m.Id == mappingId && m.ProductId == productId && m.TenantId == tenantId.Value, cancellationToken);
        if (mapping is null)
            return Results.NotFound();

        db.ProductTaxonomyMappings.Remove(mapping);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMappingAttributes(
        Guid productId,
        Guid mappingId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var mapping = await db.ProductTaxonomyMappings
            .FirstOrDefaultAsync(m => m.Id == mappingId && m.ProductId == productId && m.TenantId == tenantId.Value, cancellationToken);
        if (mapping is null)
            return Results.NotFound();

        var values = await db.ProductTaxonomyAttributeValues
            .Where(v => v.MappingId == mappingId && v.TenantId == tenantId.Value)
            .OrderBy(v => v.AttributeName)
            .ToListAsync(cancellationToken);

        return Results.Ok(values.Select(v => new
        {
            v.Id,
            v.AttributeId,
            v.AttributeName,
            v.TextValue,
            v.NumberValue,
            v.BoolValue
        }).ToList());
    }

    private static async Task<IResult> UpsertMappingAttributes(
        Guid productId,
        Guid mappingId,
        [FromBody] List<UpsertProductTaxonomyAttributeValueRequest> request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var mapping = await db.ProductTaxonomyMappings
            .FirstOrDefaultAsync(m => m.Id == mappingId && m.ProductId == productId && m.TenantId == tenantId.Value, cancellationToken);
        if (mapping is null)
            return Results.NotFound();

        var existing = await db.ProductTaxonomyAttributeValues
            .Where(v => v.MappingId == mappingId && v.TenantId == tenantId.Value)
            .ToListAsync(cancellationToken);

        foreach (var item in request)
        {
            var match = existing.FirstOrDefault(v => v.AttributeId == item.AttributeId);
            if (match is null)
            {
                db.ProductTaxonomyAttributeValues.Add(new ProductTaxonomyAttributeValue
                {
                    Id = Guid.NewGuid(),
                    MappingId = mappingId,
                    TenantId = tenantId.Value,
                    AttributeId = item.AttributeId,
                    AttributeName = item.AttributeName,
                    TextValue = item.TextValue,
                    NumberValue = item.NumberValue,
                    BoolValue = item.BoolValue
                });
            }
            else
            {
                match.AttributeName = item.AttributeName;
                match.TextValue = item.TextValue;
                match.NumberValue = item.NumberValue;
                match.BoolValue = item.BoolValue;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SuggestOptions(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext catalogDb,
        CommonTaxonomyDbContext taxonomyDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var mappings = await catalogDb.ProductTaxonomyMappings
            .Where(m => m.ProductId == productId && m.TenantId == tenantId.Value)
            .ToListAsync(cancellationToken);

        if (mappings.Count == 0)
            return Results.Ok(new List<object>());

        var nodeIds = mappings.Select(m => m.TaxonomyNodeId).ToList();
        var attributes = await taxonomyDb.NodeAttributes
            .Where(a => nodeIds.Contains(a.NodeId))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        var suggestions = attributes
            .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(a => new
            {
                attributeName = a.Name,
                valueType = a.ValueType.ToString(),
                allowedValues = a.AllowedValues
            })
            .ToList();

        return Results.Ok(suggestions);
    }
}

public record CreateProductTaxonomyMappingRequest(
    string ProviderKey,
    Guid TaxonomyNodeId,
    string NodeName,
    string NodeFullPath);

public record UpsertProductTaxonomyAttributeValueRequest(
    Guid AttributeId,
    string AttributeName,
    string? TextValue,
    decimal? NumberValue,
    bool? BoolValue);
