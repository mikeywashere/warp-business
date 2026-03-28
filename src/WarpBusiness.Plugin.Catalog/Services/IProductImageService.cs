using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.Plugin.Catalog.Services;

public interface IProductImageService
{
    Task<IReadOnlyList<ProductImageDto>> GetImagesAsync(Guid productId, CancellationToken ct = default);
    Task<ProductImageDto> AddImageAsync(Guid productId, CreateProductImageRequest request, CancellationToken ct = default);
    Task<bool> DeleteImageAsync(Guid productId, Guid imageId, CancellationToken ct = default);
}
