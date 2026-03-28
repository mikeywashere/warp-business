using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.Plugin.Catalog.Services;

public interface IProductIngredientService
{
    Task<IReadOnlyList<ProductIngredientDto>> GetIngredientsAsync(Guid productId, CancellationToken ct = default);
    Task<ProductIngredientDto> AddIngredientAsync(Guid productId, CreateProductIngredientRequest request, CancellationToken ct = default);
    Task<ProductIngredientDto?> UpdateIngredientAsync(Guid productId, Guid ingredientId, UpdateProductIngredientRequest request, CancellationToken ct = default);
    Task<bool> DeleteIngredientAsync(Guid productId, Guid ingredientId, CancellationToken ct = default);
}
