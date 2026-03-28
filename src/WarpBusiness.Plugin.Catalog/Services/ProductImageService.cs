using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Catalog.Data;
using WarpBusiness.Plugin.Catalog.Domain;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.Plugin.Catalog.Services;

public class ProductImageService : IProductImageService
{
    private readonly CatalogDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProductImageService(CatalogDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ProductImageDto>> GetImagesAsync(Guid productId, CancellationToken ct = default)
    {
        return await _db.ProductImages
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductImageDto(
                i.Id, i.ProductId, i.ProductVariantId,
                i.Url, i.FileName, i.AltText, i.ContentType, i.FileSizeBytes,
                i.IsPrimary, i.DisplayOrder, i.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ProductImageDto> AddImageAsync(Guid productId, CreateProductImageRequest request, CancellationToken ct = default)
    {
        // If this is marked as primary, unset other primaries
        if (request.IsPrimary)
        {
            var existingPrimary = await _db.ProductImages
                .Where(i => i.ProductId == productId && i.IsPrimary)
                .ToListAsync(ct);
            foreach (var img in existingPrimary)
                img.IsPrimary = false;
        }

        var image = new ProductImage
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ProductId = productId,
            ProductVariantId = request.ProductVariantId,
            Url = request.Url,
            FileName = request.FileName,
            AltText = request.AltText,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            IsPrimary = request.IsPrimary,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.ProductImages.Add(image);
        await _db.SaveChangesAsync(ct);

        return new ProductImageDto(
            image.Id, image.ProductId, image.ProductVariantId,
            image.Url, image.FileName, image.AltText, image.ContentType, image.FileSizeBytes,
            image.IsPrimary, image.DisplayOrder, image.CreatedAt);
    }

    public async Task<bool> DeleteImageAsync(Guid productId, Guid imageId, CancellationToken ct = default)
    {
        var image = await _db.ProductImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct);
        if (image is null) return false;

        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
