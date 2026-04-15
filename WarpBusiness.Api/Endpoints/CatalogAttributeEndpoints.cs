using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Api.Endpoints;

public static class CatalogAttributeEndpoints
{
    public static void MapCatalogAttributeEndpoints(this WebApplication app)
    {
        // Attribute types
        var attrTypes = app.MapGroup("/api/catalog/attribute-types").RequireAuthorization();
        attrTypes.MapGet("", GetAttributeTypes).WithName("GetCatalogAttributeTypes");
        attrTypes.MapGet("{id:guid}", GetAttributeType).WithName("GetCatalogAttributeType");
        attrTypes.MapPost("", CreateAttributeType).WithName("CreateCatalogAttributeType");
        attrTypes.MapPut("{id:guid}", UpdateAttributeType).WithName("UpdateCatalogAttributeType");
        attrTypes.MapDelete("{id:guid}", DeleteAttributeType).WithName("DeleteCatalogAttributeType");

        // Attribute options (nested under attribute type)
        var options = app.MapGroup("/api/catalog/attribute-types/{attributeTypeId:guid}/options").RequireAuthorization();
        options.MapGet("", GetAttributeOptions).WithName("GetCatalogAttributeOptions");
        options.MapPost("", CreateAttributeOption).WithName("CreateCatalogAttributeOption");
        options.MapPut("{optionId:guid}", UpdateAttributeOption).WithName("UpdateCatalogAttributeOption");
        options.MapDelete("{optionId:guid}", DeleteAttributeOption).WithName("DeleteCatalogAttributeOption");
    }

    // ── Attribute Types ────────────────────────────────────────────────────────

    private static async Task<IResult> GetAttributeTypes(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var types = await db.AttributeTypes
            .Where(at => at.TenantId == tenantId.Value)
            .Include(at => at.Options.Where(o => o.IsActive))
            .OrderBy(at => at.SortOrder)
            .ThenBy(at => at.Name)
            .ToListAsync(cancellationToken);

        return Results.Ok(types.Select(MapAttrTypeToResponse).ToList());
    }

    private static async Task<IResult> GetAttributeType(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var attrType = await db.AttributeTypes
            .Where(at => at.Id == id && at.TenantId == tenantId.Value)
            .Include(at => at.Options)
            .FirstOrDefaultAsync(cancellationToken);

        return attrType is null ? Results.NotFound() : Results.Ok(MapAttrTypeToResponse(attrType));
    }

    private static async Task<IResult> CreateAttributeType(
        [FromBody] CreateAttributeTypeRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Attribute type name is required." });

        if (!Enum.TryParse<AttributeValueType>(request.ValueType, ignoreCase: true, out var valueType))
            return Results.BadRequest(new { message = $"Invalid value type '{request.ValueType}'. Valid values: Select, FreeText, Number." });

        var attrType = new CatalogAttributeType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            ValueType = valueType,
            Unit = request.Unit,
            HasColorPicker = request.HasColorPicker,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.AttributeTypes.Add(attrType);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An attribute type with this name already exists." });
        }

        return Results.Created(
            $"/api/catalog/attribute-types/{attrType.Id}",
            MapAttrTypeToResponse(attrType));
    }

    private static async Task<IResult> UpdateAttributeType(
        Guid id,
        [FromBody] UpdateAttributeTypeRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var attrType = await db.AttributeTypes
            .Where(at => at.Id == id && at.TenantId == tenantId.Value)
            .Include(at => at.Options)
            .FirstOrDefaultAsync(cancellationToken);
        if (attrType is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Attribute type name is required." });

        attrType.Name = request.Name;
        attrType.Unit = request.Unit;
        attrType.HasColorPicker = request.HasColorPicker ?? attrType.HasColorPicker;
        attrType.SortOrder = request.SortOrder ?? attrType.SortOrder;
        attrType.IsActive = request.IsActive ?? attrType.IsActive;
        attrType.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An attribute type with this name already exists." });
        }

        return Results.Ok(MapAttrTypeToResponse(attrType));
    }

    private static async Task<IResult> DeleteAttributeType(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var attrType = await db.AttributeTypes
            .FirstOrDefaultAsync(at => at.Id == id && at.TenantId == tenantId.Value, cancellationToken);
        if (attrType is null)
            return Results.NotFound();

        var inUse = await db.VariantAttributeValues.AnyAsync(v => v.AttributeTypeId == id, cancellationToken);
        if (inUse)
        {
            attrType.IsActive = false;
            attrType.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Attribute type is in use by product variants and was deactivated instead of deleted." });
        }

        db.AttributeTypes.Remove(attrType);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // ── Attribute Options ──────────────────────────────────────────────────────

    private static async Task<IResult> GetAttributeOptions(
        Guid attributeTypeId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var exists = await db.AttributeTypes
            .AnyAsync(at => at.Id == attributeTypeId && at.TenantId == tenantId.Value, cancellationToken);
        if (!exists)
            return Results.NotFound();

        var options = await db.AttributeOptions
            .Where(o => o.AttributeTypeId == attributeTypeId && o.TenantId == tenantId.Value)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Value)
            .Select(o => new AttributeOptionResponse(
                o.Id, o.AttributeTypeId, o.TenantId, o.Value, o.HexCode, o.SortOrder, o.IsActive, o.CreatedAt, o.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(options);
    }

    private static async Task<IResult> CreateAttributeOption(
        Guid attributeTypeId,
        [FromBody] CreateAttributeOptionRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var attrType = await db.AttributeTypes
            .FirstOrDefaultAsync(at => at.Id == attributeTypeId && at.TenantId == tenantId.Value, cancellationToken);
        if (attrType is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Value))
            return Results.BadRequest(new { message = "Option value is required." });

        var option = new CatalogAttributeOption
        {
            Id = Guid.NewGuid(),
            AttributeTypeId = attributeTypeId,
            TenantId = tenantId.Value,
            Value = request.Value,
            HexCode = request.HexCode,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.AttributeOptions.Add(option);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An option with this value already exists for this attribute type." });
        }

        return Results.Created(
            $"/api/catalog/attribute-types/{attributeTypeId}/options/{option.Id}",
            new AttributeOptionResponse(option.Id, option.AttributeTypeId, option.TenantId,
                option.Value, option.HexCode, option.SortOrder, option.IsActive, option.CreatedAt, option.UpdatedAt));
    }

    private static async Task<IResult> UpdateAttributeOption(
        Guid attributeTypeId,
        Guid optionId,
        [FromBody] UpdateAttributeOptionRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var option = await db.AttributeOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.AttributeTypeId == attributeTypeId && o.TenantId == tenantId.Value, cancellationToken);
        if (option is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Value))
            return Results.BadRequest(new { message = "Option value is required." });

        option.Value = request.Value;
        option.HexCode = request.HexCode;
        option.SortOrder = request.SortOrder ?? option.SortOrder;
        option.IsActive = request.IsActive ?? option.IsActive;
        option.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An option with this value already exists for this attribute type." });
        }

        return Results.Ok(new AttributeOptionResponse(option.Id, option.AttributeTypeId, option.TenantId,
            option.Value, option.HexCode, option.SortOrder, option.IsActive, option.CreatedAt, option.UpdatedAt));
    }

    private static async Task<IResult> DeleteAttributeOption(
        Guid attributeTypeId,
        Guid optionId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var option = await db.AttributeOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.AttributeTypeId == attributeTypeId && o.TenantId == tenantId.Value, cancellationToken);
        if (option is null)
            return Results.NotFound();

        var inUse = await db.VariantAttributeValues.AnyAsync(v => v.AttributeOptionId == optionId, cancellationToken);
        if (inUse)
        {
            option.IsActive = false;
            option.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Option is in use by product variants and was deactivated instead of deleted." });
        }

        db.AttributeOptions.Remove(option);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static AttributeTypeResponse MapAttrTypeToResponse(CatalogAttributeType at) => new(
        at.Id, at.TenantId, at.Name, at.ValueType.ToString(), at.Unit, at.HasColorPicker,
        at.SortOrder, at.IsActive, at.CreatedAt, at.UpdatedAt,
        at.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Value).Select(o =>
            new AttributeOptionResponse(o.Id, o.AttributeTypeId, o.TenantId,
                o.Value, o.HexCode, o.SortOrder, o.IsActive, o.CreatedAt, o.UpdatedAt)).ToList());
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record AttributeTypeResponse(
    Guid Id, Guid TenantId, string Name, string ValueType, string? Unit, bool HasColorPicker,
    int SortOrder, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    List<AttributeOptionResponse> Options);

public record AttributeOptionResponse(
    Guid Id, Guid AttributeTypeId, Guid TenantId, string Value, string? HexCode,
    int SortOrder, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateAttributeTypeRequest(
    string Name,
    string ValueType,
    string? Unit = null,
    bool HasColorPicker = false,
    int SortOrder = 0);

public record UpdateAttributeTypeRequest(
    string Name,
    string? Unit = null,
    bool? HasColorPicker = null,
    int? SortOrder = null,
    bool? IsActive = null);

public record CreateAttributeOptionRequest(string Value, string? HexCode = null, int SortOrder = 0);
public record UpdateAttributeOptionRequest(string Value, string? HexCode = null, int? SortOrder = null, bool? IsActive = null);
