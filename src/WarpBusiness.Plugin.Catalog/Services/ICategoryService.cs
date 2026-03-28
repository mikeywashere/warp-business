using WarpBusiness.Shared.Catalog;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Catalog.Services;

public enum DeleteCategoryResult { Deleted, NotFound, HasProducts, HasSubCategories }

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetCategoriesAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>> GetAllCategoriesAsync(CancellationToken ct = default);
    Task<CategoryDetailDto?> GetCategoryDetailAsync(Guid id, CancellationToken ct = default);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, string userId, CancellationToken ct = default);
    Task<CategoryDto?> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<DeleteCategoryResult> DeleteCategoryAsync(Guid id, CancellationToken ct = default);
}
