using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Catalog.Data;
using WarpBusiness.Plugin.Catalog.Domain;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.Plugin.Catalog.Services;

public class ProductVariantService : IProductVariantService
{
    private readonly CatalogDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProductVariantService(CatalogDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ProductVariantDto>> GetVariantsAsync(Guid productId, CancellationToken ct = default)
    {
        return await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.OptionValues).ThenInclude(ov => ov.ProductOptionValue)
                .ThenInclude(pov => pov!.ProductOption)
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.DisplayOrder)
            .Select(v => new ProductVariantDto(
                v.Id, v.ProductId, v.Sku, v.Barcode,
                v.Price, v.CostPrice, v.Weight,
                v.StockQuantity, v.LowStockThreshold, v.TrackInventory,
                v.IsActive, v.DisplayOrder,
                v.OptionValues
                    .Where(ov => ov.ProductOptionValue != null)
                    .Select(ov => new VariantOptionValueDto(
                        ov.ProductOptionValue!.ProductOption != null ? ov.ProductOptionValue.ProductOption.Name : "",
                        ov.ProductOptionValue.Value))
                    .ToList(),
                v.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ProductVariantDto> CreateVariantAsync(Guid productId, CreateProductVariantRequest request, CancellationToken ct = default)
    {
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ProductId = productId,
            Sku = request.Sku,
            Barcode = request.Barcode,
            Price = request.Price,
            CostPrice = request.CostPrice,
            Weight = request.Weight,
            StockQuantity = request.StockQuantity,
            LowStockThreshold = request.LowStockThreshold,
            TrackInventory = request.TrackInventory,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.ProductVariants.Add(variant);

        // Link option values if provided
        if (request.OptionValueIds is { Count: > 0 })
        {
            foreach (var optValId in request.OptionValueIds)
            {
                _db.VariantOptionValues.Add(new VariantOptionValue
                {
                    ProductVariantId = variant.Id,
                    ProductOptionValueId = optValId,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ProductVariantDto(
            variant.Id, variant.ProductId, variant.Sku, variant.Barcode,
            variant.Price, variant.CostPrice, variant.Weight,
            variant.StockQuantity, variant.LowStockThreshold, variant.TrackInventory,
            variant.IsActive, variant.DisplayOrder,
            [], variant.CreatedAt);
    }

    public async Task<ProductVariantDto?> UpdateVariantAsync(Guid productId, Guid variantId, UpdateProductVariantRequest request, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId, ct);
        if (variant is null) return null;

        variant.Sku = request.Sku;
        variant.Barcode = request.Barcode;
        variant.Price = request.Price;
        variant.CostPrice = request.CostPrice;
        variant.Weight = request.Weight;
        variant.StockQuantity = request.StockQuantity;
        variant.LowStockThreshold = request.LowStockThreshold;
        variant.TrackInventory = request.TrackInventory;
        variant.IsActive = request.IsActive;
        variant.DisplayOrder = request.DisplayOrder;
        variant.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ProductVariantDto(
            variant.Id, variant.ProductId, variant.Sku, variant.Barcode,
            variant.Price, variant.CostPrice, variant.Weight,
            variant.StockQuantity, variant.LowStockThreshold, variant.TrackInventory,
            variant.IsActive, variant.DisplayOrder,
            [], variant.CreatedAt);
    }

    public async Task<bool> DeleteVariantAsync(Guid productId, Guid variantId, CancellationToken ct = default)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId, ct);
        if (variant is null) return false;

        _db.ProductVariants.Remove(variant);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ProductOptionDto> AddOptionAsync(Guid productId, CreateProductOptionRequest request, CancellationToken ct = default)
    {
        var option = new ProductOption
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ProductId = productId,
            Name = request.Name,
            DisplayOrder = request.DisplayOrder,
        };

        _db.ProductOptions.Add(option);

        var optionValues = new List<ProductOptionValue>();
        if (request.Values is { Count: > 0 })
        {
            for (int i = 0; i < request.Values.Count; i++)
            {
                var ov = new ProductOptionValue
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantContext.TenantId,
                    ProductOptionId = option.Id,
                    Value = request.Values[i],
                    DisplayOrder = i,
                };
                _db.ProductOptionValues.Add(ov);
                optionValues.Add(ov);
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ProductOptionDto(
            option.Id, option.Name, option.DisplayOrder,
            optionValues.Select(v => new ProductOptionValueDto(v.Id, v.Value, v.DisplayOrder)).ToList());
    }

    public async Task<IReadOnlyList<ProductOptionDto>> GetOptionsAsync(Guid productId, CancellationToken ct = default)
    {
        return await _db.ProductOptions
            .AsNoTracking()
            .Include(o => o.Values)
            .Where(o => o.ProductId == productId)
            .OrderBy(o => o.DisplayOrder)
            .Select(o => new ProductOptionDto(
                o.Id, o.Name, o.DisplayOrder,
                o.Values.OrderBy(v => v.DisplayOrder)
                    .Select(v => new ProductOptionValueDto(v.Id, v.Value, v.DisplayOrder))
                    .ToList()))
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteOptionAsync(Guid productId, Guid optionId, CancellationToken ct = default)
    {
        var option = await _db.ProductOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.ProductId == productId, ct);
        if (option is null) return false;

        _db.ProductOptions.Remove(option);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
