using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Api.Endpoints;

public static class CatalogProductTypeEndpoints
{
    public static void MapCatalogProductTypeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog/product-types").RequireAuthorization();
        group.MapGet("", GetProductTypes).WithName("GetCatalogProductTypes");
        group.MapGet("{id:guid}", GetProductType).WithName("GetCatalogProductType");
        group.MapPost("", CreateProductType).WithName("CreateCatalogProductType");
        group.MapPut("{id:guid}", UpdateProductType).WithName("UpdateCatalogProductType");
        group.MapDelete("{id:guid}", DeleteProductType).WithName("DeleteCatalogProductType");

        // Attribute assignments for a product type
        group.MapGet("{id:guid}/attributes", GetProductTypeAttributes).WithName("GetCatalogProductTypeAttributes");
        group.MapPost("{id:guid}/attributes", AssignAttribute).WithName("AssignCatalogProductTypeAttribute");
        group.MapDelete("{id:guid}/attributes/{attributeTypeId:guid}", RemoveAttribute).WithName("RemoveCatalogProductTypeAttribute");
    }

    private static async Task<IResult> GetProductTypes(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var types = await db.ProductTypes
            .Where(pt => pt.TenantId == tenantId.Value)
            .Include(pt => pt.Attributes)
            .ThenInclude(pta => pta.AttributeType)
            .OrderBy(pt => pt.Name)
            .ToListAsync(cancellationToken);

        return Results.Ok(types.Select(MapToResponse).ToList());
    }

    private static async Task<IResult> GetProductType(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productType = await db.ProductTypes
            .Where(pt => pt.Id == id && pt.TenantId == tenantId.Value)
            .Include(pt => pt.Attributes)
            .ThenInclude(pta => pta.AttributeType)
            .FirstOrDefaultAsync(cancellationToken);

        return productType is null ? Results.NotFound() : Results.Ok(MapToResponse(productType));
    }

    private static async Task<IResult> CreateProductType(
        [FromBody] CreateProductTypeRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Product type name is required." });

        var productType = new ProductType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.ProductTypes.Add(productType);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A product type with this name already exists." });
        }

        return Results.Created(
            $"/api/catalog/product-types/{productType.Id}",
            MapToResponse(productType));
    }

    private static async Task<IResult> UpdateProductType(
        Guid id,
        [FromBody] UpdateProductTypeRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productType = await db.ProductTypes
            .Where(pt => pt.Id == id && pt.TenantId == tenantId.Value)
            .Include(pt => pt.Attributes)
            .ThenInclude(pta => pta.AttributeType)
            .FirstOrDefaultAsync(cancellationToken);
        if (productType is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Product type name is required." });

        productType.Name = request.Name;
        productType.Description = request.Description;
        productType.IsActive = request.IsActive ?? productType.IsActive;
        productType.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A product type with this name already exists." });
        }

        return Results.Ok(MapToResponse(productType));
    }

    private static async Task<IResult> DeleteProductType(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productType = await db.ProductTypes
            .FirstOrDefaultAsync(pt => pt.Id == id && pt.TenantId == tenantId.Value, cancellationToken);
        if (productType is null)
            return Results.NotFound();

        var inUse = await db.Products.AnyAsync(p => p.ProductTypeId == id, cancellationToken);
        if (inUse)
        {
            productType.IsActive = false;
            productType.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Product type is used by products and was deactivated instead of deleted." });
        }

        db.ProductTypes.Remove(productType);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetProductTypeAttributes(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var exists = await db.ProductTypes.AnyAsync(pt => pt.Id == id && pt.TenantId == tenantId.Value, cancellationToken);
        if (!exists)
            return Results.NotFound();

        var attributes = await db.ProductTypeAttributes
            .Where(pta => pta.ProductTypeId == id)
            .Include(pta => pta.AttributeType)
            .OrderBy(pta => pta.SortOrder)
            .ThenBy(pta => pta.AttributeType.Name)
            .Select(pta => new ProductTypeAttributeResponse(
                pta.AttributeTypeId,
                pta.AttributeType.Name,
                pta.AttributeType.ValueType.ToString(),
                pta.AttributeType.Unit,
                pta.AttributeType.HasColorPicker,
                pta.IsRequired,
                pta.SortOrder))
            .ToListAsync(cancellationToken);

        return Results.Ok(attributes);
    }

    private static async Task<IResult> AssignAttribute(
        Guid id,
        [FromBody] AssignProductTypeAttributeRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productType = await db.ProductTypes
            .FirstOrDefaultAsync(pt => pt.Id == id && pt.TenantId == tenantId.Value, cancellationToken);
        if (productType is null)
            return Results.NotFound();

        var attrType = await db.AttributeTypes
            .FirstOrDefaultAsync(at => at.Id == request.AttributeTypeId && at.TenantId == tenantId.Value, cancellationToken);
        if (attrType is null)
            return Results.BadRequest(new { message = "Attribute type not found in this tenant." });

        var existing = await db.ProductTypeAttributes
            .AnyAsync(pta => pta.ProductTypeId == id && pta.AttributeTypeId == request.AttributeTypeId, cancellationToken);
        if (existing)
            return Results.Conflict(new { message = "This attribute type is already assigned to this product type." });

        db.ProductTypeAttributes.Add(new ProductTypeAttribute
        {
            ProductTypeId = id,
            AttributeTypeId = request.AttributeTypeId,
            IsRequired = request.IsRequired,
            SortOrder = request.SortOrder
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/catalog/product-types/{id}/attributes",
            new ProductTypeAttributeResponse(
                attrType.Id, attrType.Name, attrType.ValueType.ToString(),
                attrType.Unit, attrType.HasColorPicker, request.IsRequired, request.SortOrder));
    }

    private static async Task<IResult> RemoveAttribute(
        Guid id,
        Guid attributeTypeId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var exists = await db.ProductTypes.AnyAsync(pt => pt.Id == id && pt.TenantId == tenantId.Value, cancellationToken);
        if (!exists)
            return Results.NotFound();

        var pta = await db.ProductTypeAttributes
            .FirstOrDefaultAsync(a => a.ProductTypeId == id && a.AttributeTypeId == attributeTypeId, cancellationToken);
        if (pta is null)
            return Results.NotFound();

        db.ProductTypeAttributes.Remove(pta);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static ProductTypeResponse MapToResponse(ProductType pt) => new(
        pt.Id, pt.TenantId, pt.Name, pt.Description, pt.IsActive, pt.CreatedAt, pt.UpdatedAt,
        pt.Attributes.OrderBy(a => a.SortOrder).ThenBy(a => a.AttributeType?.Name ?? "").Select(a =>
            new ProductTypeAttributeResponse(
                a.AttributeTypeId,
                a.AttributeType?.Name ?? "",
                a.AttributeType?.ValueType.ToString() ?? "",
                a.AttributeType?.Unit,
                a.AttributeType?.HasColorPicker ?? false,
                a.IsRequired,
                a.SortOrder)).ToList());
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ProductTypeResponse(
    Guid Id, Guid TenantId, string Name, string? Description, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    List<ProductTypeAttributeResponse> Attributes);

public record ProductTypeAttributeResponse(
    Guid AttributeTypeId, string AttributeTypeName, string ValueType,
    string? Unit, bool HasColorPicker, bool IsRequired, int SortOrder);

public record CreateProductTypeRequest(string Name, string? Description = null);
public record UpdateProductTypeRequest(string Name, string? Description = null, bool? IsActive = null);
public record AssignProductTypeAttributeRequest(Guid AttributeTypeId, bool IsRequired = false, int SortOrder = 0);
