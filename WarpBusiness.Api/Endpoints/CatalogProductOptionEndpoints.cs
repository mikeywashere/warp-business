using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;
using WarpBusiness.Catalog.Services;

namespace WarpBusiness.Api.Endpoints;

public static class CatalogProductOptionEndpoints
{
    public static void MapCatalogProductOptionEndpoints(this WebApplication app)
    {
        var options = app.MapGroup("/api/catalog/products/{productId:guid}/options").RequireAuthorization();
        options.MapGet("", GetOptions).WithName("GetCatalogProductOptions");
        options.MapPost("", CreateOption).WithName("CreateCatalogProductOption");
        options.MapPut("{optionId:guid}", UpdateOption).WithName("UpdateCatalogProductOption");
        options.MapDelete("{optionId:guid}", DeleteOption).WithName("DeleteCatalogProductOption");

        options.MapPost("{optionId:guid}/values", CreateOptionValue).WithName("CreateCatalogProductOptionValue");
        options.MapPut("{optionId:guid}/values/{valueId:guid}", UpdateOptionValue).WithName("UpdateCatalogProductOptionValue");
        options.MapDelete("{optionId:guid}/values/{valueId:guid}", DeleteOptionValue).WithName("DeleteCatalogProductOptionValue");

        var variants = app.MapGroup("/api/catalog/products/{productId:guid}/variants").RequireAuthorization();
        variants.MapPost("generate", GenerateVariants).WithName("GenerateCatalogProductVariants");
    }

    private static async Task<IResult> GetOptions(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productExists = await db.Products.AnyAsync(
            p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (!productExists)
            return Results.NotFound();

        var options = await db.ProductOptions
            .Where(o => o.ProductId == productId && o.TenantId == tenantId.Value)
            .Include(o => o.Values)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Name)
            .ToListAsync(cancellationToken);

        return Results.Ok(options.Select(MapOptionResponse).ToList());
    }

    private static async Task<IResult> CreateOption(
        Guid productId,
        [FromBody] CreateProductOptionRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Option name is required." });

        var productExists = await db.Products.AnyAsync(
            p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (!productExists)
            return Results.NotFound();

        var option = new ProductOption
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            TenantId = tenantId.Value,
            Name = request.Name,
            SortOrder = request.SortOrder,
            IsVariantAxis = request.IsVariantAxis
        };

        db.ProductOptions.Add(option);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An option with this name already exists for this product." });
        }

        return Results.Created(
            $"/api/catalog/products/{productId}/options/{option.Id}",
            MapOptionResponse(option));
    }

    private static async Task<IResult> UpdateOption(
        Guid productId,
        Guid optionId,
        [FromBody] UpdateProductOptionRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var option = await db.ProductOptions
            .Include(o => o.Values)
            .FirstOrDefaultAsync(o => o.Id == optionId && o.ProductId == productId && o.TenantId == tenantId.Value, cancellationToken);
        if (option is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Option name is required." });

        option.Name = request.Name;
        option.SortOrder = request.SortOrder;
        option.IsVariantAxis = request.IsVariantAxis;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An option with this name already exists for this product." });
        }

        return Results.Ok(MapOptionResponse(option));
    }

    private static async Task<IResult> DeleteOption(
        Guid productId,
        Guid optionId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var option = await db.ProductOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.ProductId == productId && o.TenantId == tenantId.Value, cancellationToken);
        if (option is null)
            return Results.NotFound();

        db.ProductOptions.Remove(option);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateOptionValue(
        Guid productId,
        Guid optionId,
        [FromBody] CreateProductOptionValueRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Value))
            return Results.BadRequest(new { message = "Option value is required." });

        var option = await db.ProductOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.ProductId == productId && o.TenantId == tenantId.Value, cancellationToken);
        if (option is null)
            return Results.NotFound();

        var value = new ProductOptionValue
        {
            Id = Guid.NewGuid(),
            OptionId = optionId,
            ProductId = productId,
            TenantId = tenantId.Value,
            Value = request.Value,
            HexCode = request.HexCode,
            SortOrder = request.SortOrder
        };

        db.ProductOptionValues.Add(value);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An option value with this name already exists." });
        }

        return Results.Created(
            $"/api/catalog/products/{productId}/options/{optionId}/values/{value.Id}",
            MapOptionValueResponse(value));
    }

    private static async Task<IResult> UpdateOptionValue(
        Guid productId,
        Guid optionId,
        Guid valueId,
        [FromBody] UpdateProductOptionValueRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var value = await db.ProductOptionValues
            .FirstOrDefaultAsync(v => v.Id == valueId && v.OptionId == optionId && v.ProductId == productId && v.TenantId == tenantId.Value, cancellationToken);
        if (value is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Value))
            return Results.BadRequest(new { message = "Option value is required." });

        value.Value = request.Value;
        value.HexCode = request.HexCode;
        value.SortOrder = request.SortOrder;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An option value with this name already exists." });
        }

        return Results.Ok(MapOptionValueResponse(value));
    }

    private static async Task<IResult> DeleteOptionValue(
        Guid productId,
        Guid optionId,
        Guid valueId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var value = await db.ProductOptionValues
            .FirstOrDefaultAsync(v => v.Id == valueId && v.OptionId == optionId && v.ProductId == productId && v.TenantId == tenantId.Value, cancellationToken);
        if (value is null)
            return Results.NotFound();

        db.ProductOptionValues.Remove(value);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GenerateVariants(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext db,
        VariantGenerationService generator,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productExists = await db.Products.AnyAsync(
            p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (!productExists)
            return Results.NotFound();

        var created = await generator.GenerateVariantsAsync(productId, tenantId.Value, cancellationToken);
        return Results.Ok(new { created });
    }

    private static ProductOptionResponse MapOptionResponse(ProductOption option) => new(
        option.Id,
        option.ProductId,
        option.Name,
        option.SortOrder,
        option.IsVariantAxis,
        option.Values.OrderBy(v => v.SortOrder).ThenBy(v => v.Value).Select(MapOptionValueResponse).ToList());

    private static ProductOptionValueResponse MapOptionValueResponse(ProductOptionValue value) => new(
        value.Id,
        value.Value,
        value.HexCode,
        value.SortOrder);
}

public record ProductOptionResponse(
    Guid Id,
    Guid ProductId,
    string Name,
    int SortOrder,
    bool IsVariantAxis,
    List<ProductOptionValueResponse> Values);

public record ProductOptionValueResponse(
    Guid Id,
    string Value,
    string? HexCode,
    int SortOrder);

public record CreateProductOptionRequest(
    string Name,
    int SortOrder = 0,
    bool IsVariantAxis = true);

public record UpdateProductOptionRequest(
    string Name,
    int SortOrder = 0,
    bool IsVariantAxis = true);

public record CreateProductOptionValueRequest(
    string Value,
    string? HexCode = null,
    int SortOrder = 0);

public record UpdateProductOptionValueRequest(
    string Value,
    string? HexCode = null,
    int SortOrder = 0);
