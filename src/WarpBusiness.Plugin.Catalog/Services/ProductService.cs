using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Catalog.Data;
using WarpBusiness.Plugin.Catalog.Domain;
using WarpBusiness.Shared.Catalog;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Catalog.Services;

public class ProductService : IProductService
{
    private readonly CatalogDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProductService(CatalogDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(
        int page, int pageSize, string? search, Guid? categoryId, string? status, CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                (p.Sku != null && p.Sku.ToLower().Contains(term)) ||
                (p.Brand != null && p.Brand.ToLower().Contains(term)));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProductStatus>(status, true, out var statusEnum))
            query = query.Where(p => p.Status == statusEnum);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.ShortDescription, p.Sku, p.Brand,
                p.ProductType.ToString(), p.Status.ToString(),
                p.BasePrice, p.Currency,
                p.CategoryId,
                p.Category != null ? p.Category.Name : null,
                p.Images.Where(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
                p.Variants.Count, p.Images.Count,
                p.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(items, totalCount, page, pageSize);
    }

    public async Task<ProductDetailDto?> GetProductDetailAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Options).ThenInclude(o => o.Values)
            .Include(p => p.Variants).ThenInclude(v => v.OptionValues).ThenInclude(ov => ov.ProductOptionValue)
                .ThenInclude(pov => pov!.ProductOption)
            .Include(p => p.Images)
            .Include(p => p.Ingredients)
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync(ct);

        if (product is null) return null;

        var options = product.Options
            .OrderBy(o => o.DisplayOrder)
            .Select(o => new ProductOptionDto(
                o.Id, o.Name, o.DisplayOrder,
                o.Values.OrderBy(v => v.DisplayOrder)
                    .Select(v => new ProductOptionValueDto(v.Id, v.Value, v.DisplayOrder))
                    .ToList()))
            .ToList();

        var variants = product.Variants
            .OrderBy(v => v.DisplayOrder)
            .Select(v => new ProductVariantDto(
                v.Id, v.ProductId, v.Sku, v.Barcode,
                v.Price, v.CostPrice, v.Weight,
                v.StockQuantity, v.LowStockThreshold, v.TrackInventory,
                v.IsActive, v.DisplayOrder,
                v.OptionValues
                    .Where(ov => ov.ProductOptionValue != null)
                    .Select(ov => new VariantOptionValueDto(
                        ov.ProductOptionValue!.ProductOption?.Name ?? "",
                        ov.ProductOptionValue.Value))
                    .ToList(),
                v.CreatedAt))
            .ToList();

        var images = product.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductImageDto(
                i.Id, i.ProductId, i.ProductVariantId,
                i.Url, i.FileName, i.AltText, i.ContentType, i.FileSizeBytes,
                i.IsPrimary, i.DisplayOrder, i.CreatedAt))
            .ToList();

        var ingredients = product.Ingredients
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductIngredientDto(
                i.Id, i.ProductId, i.Name,
                i.Quantity, i.Unit, i.IsAllergen, i.AllergenType,
                i.DisplayOrder, i.Notes))
            .ToList();

        return new ProductDetailDto(
            product.Id, product.Name, product.Description, product.ShortDescription,
            product.Slug, product.Sku, product.Barcode,
            product.Brand, product.Manufacturer,
            product.CategoryId,
            product.Category?.Name,
            product.ProductType.ToString(), product.Status.ToString(),
            product.BasePrice, product.CompareAtPrice, product.CostPrice, product.Currency,
            product.Weight, product.WeightUnit,
            product.Length, product.Width, product.Height, product.DimensionUnit,
            product.IsTaxable, product.TaxCode,
            product.MetaTitle, product.MetaDescription,
            product.Tags,
            product.CreatedAt, product.UpdatedAt,
            options, variants, images, ingredients);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, string userId, CancellationToken ct = default)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            Name = request.Name,
            Description = request.Description,
            ShortDescription = request.ShortDescription,
            Slug = request.Slug,
            Sku = request.Sku,
            Barcode = request.Barcode,
            Brand = request.Brand,
            Manufacturer = request.Manufacturer,
            CategoryId = request.CategoryId,
            ProductType = Enum.TryParse<ProductType>(request.ProductType, true, out var pt) ? pt : ProductType.General,
            Status = Enum.TryParse<ProductStatus>(request.Status, true, out var ps) ? ps : ProductStatus.Draft,
            BasePrice = request.BasePrice,
            CompareAtPrice = request.CompareAtPrice,
            CostPrice = request.CostPrice,
            Currency = request.Currency,
            Weight = request.Weight,
            WeightUnit = request.WeightUnit,
            Length = request.Length,
            Width = request.Width,
            Height = request.Height,
            DimensionUnit = request.DimensionUnit,
            IsTaxable = request.IsTaxable,
            TaxCode = request.TaxCode,
            MetaTitle = request.MetaTitle,
            MetaDescription = request.MetaDescription,
            Tags = request.Tags,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        return new ProductDto(
            product.Id, product.Name, product.ShortDescription, product.Sku, product.Brand,
            product.ProductType.ToString(), product.Status.ToString(),
            product.BasePrice, product.Currency,
            product.CategoryId, null, null,
            0, 0, product.CreatedAt);
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync([id], ct);
        if (product is null) return null;

        product.Name = request.Name;
        product.Description = request.Description;
        product.ShortDescription = request.ShortDescription;
        product.Slug = request.Slug;
        product.Sku = request.Sku;
        product.Barcode = request.Barcode;
        product.Brand = request.Brand;
        product.Manufacturer = request.Manufacturer;
        product.CategoryId = request.CategoryId;
        product.ProductType = Enum.TryParse<ProductType>(request.ProductType, true, out var pt) ? pt : product.ProductType;
        product.Status = Enum.TryParse<ProductStatus>(request.Status, true, out var ps) ? ps : product.Status;
        product.BasePrice = request.BasePrice;
        product.CompareAtPrice = request.CompareAtPrice;
        product.CostPrice = request.CostPrice;
        product.Currency = request.Currency;
        product.Weight = request.Weight;
        product.WeightUnit = request.WeightUnit;
        product.Length = request.Length;
        product.Width = request.Width;
        product.Height = request.Height;
        product.DimensionUnit = request.DimensionUnit;
        product.IsTaxable = request.IsTaxable;
        product.TaxCode = request.TaxCode;
        product.MetaTitle = request.MetaTitle;
        product.MetaDescription = request.MetaDescription;
        product.Tags = request.Tags;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var categoryName = product.CategoryId.HasValue
            ? await _db.Categories.Where(c => c.Id == product.CategoryId).Select(c => c.Name).FirstOrDefaultAsync(ct)
            : null;

        return new ProductDto(
            product.Id, product.Name, product.ShortDescription, product.Sku, product.Brand,
            product.ProductType.ToString(), product.Status.ToString(),
            product.BasePrice, product.Currency,
            product.CategoryId, categoryName, null,
            0, 0, product.CreatedAt);
    }

    public async Task<DeleteProductResult> DeleteProductAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync([id], ct);
        if (product is null) return DeleteProductResult.NotFound;

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);
        return DeleteProductResult.Deleted;
    }
}
