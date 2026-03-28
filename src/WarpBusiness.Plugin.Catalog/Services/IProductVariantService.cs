using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.Plugin.Catalog.Services;

public interface IProductVariantService
{
    Task<IReadOnlyList<ProductVariantDto>> GetVariantsAsync(Guid productId, CancellationToken ct = default);
    Task<ProductVariantDto> CreateVariantAsync(Guid productId, CreateProductVariantRequest request, CancellationToken ct = default);
    Task<ProductVariantDto?> UpdateVariantAsync(Guid productId, Guid variantId, UpdateProductVariantRequest request, CancellationToken ct = default);
    Task<bool> DeleteVariantAsync(Guid productId, Guid variantId, CancellationToken ct = default);
    Task<ProductOptionDto> AddOptionAsync(Guid productId, CreateProductOptionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ProductOptionDto>> GetOptionsAsync(Guid productId, CancellationToken ct = default);
    Task<bool> DeleteOptionAsync(Guid productId, Guid optionId, CancellationToken ct = default);
}
