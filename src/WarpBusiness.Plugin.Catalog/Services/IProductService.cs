using WarpBusiness.Shared.Catalog;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Catalog.Services;

public enum DeleteProductResult { Deleted, NotFound }

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(int page, int pageSize, string? search, Guid? categoryId, string? status, CancellationToken ct = default);
    Task<ProductDetailDto?> GetProductDetailAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, string userId, CancellationToken ct = default);
    Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task<DeleteProductResult> DeleteProductAsync(Guid id, CancellationToken ct = default);
}
