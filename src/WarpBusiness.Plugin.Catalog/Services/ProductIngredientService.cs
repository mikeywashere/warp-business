using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Catalog.Data;
using WarpBusiness.Plugin.Catalog.Domain;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.Plugin.Catalog.Services;

public class ProductIngredientService : IProductIngredientService
{
    private readonly CatalogDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProductIngredientService(CatalogDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ProductIngredientDto>> GetIngredientsAsync(Guid productId, CancellationToken ct = default)
    {
        return await _db.ProductIngredients
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductIngredientDto(
                i.Id, i.ProductId, i.Name,
                i.Quantity, i.Unit, i.IsAllergen, i.AllergenType,
                i.DisplayOrder, i.Notes))
            .ToListAsync(ct);
    }

    public async Task<ProductIngredientDto> AddIngredientAsync(Guid productId, CreateProductIngredientRequest request, CancellationToken ct = default)
    {
        var ingredient = new ProductIngredient
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            ProductId = productId,
            Name = request.Name,
            Quantity = request.Quantity,
            Unit = request.Unit,
            IsAllergen = request.IsAllergen,
            AllergenType = request.AllergenType,
            DisplayOrder = request.DisplayOrder,
            Notes = request.Notes,
        };

        _db.ProductIngredients.Add(ingredient);
        await _db.SaveChangesAsync(ct);

        return new ProductIngredientDto(
            ingredient.Id, ingredient.ProductId, ingredient.Name,
            ingredient.Quantity, ingredient.Unit, ingredient.IsAllergen, ingredient.AllergenType,
            ingredient.DisplayOrder, ingredient.Notes);
    }

    public async Task<ProductIngredientDto?> UpdateIngredientAsync(Guid productId, Guid ingredientId, UpdateProductIngredientRequest request, CancellationToken ct = default)
    {
        var ingredient = await _db.ProductIngredients
            .FirstOrDefaultAsync(i => i.Id == ingredientId && i.ProductId == productId, ct);
        if (ingredient is null) return null;

        ingredient.Name = request.Name;
        ingredient.Quantity = request.Quantity;
        ingredient.Unit = request.Unit;
        ingredient.IsAllergen = request.IsAllergen;
        ingredient.AllergenType = request.AllergenType;
        ingredient.DisplayOrder = request.DisplayOrder;
        ingredient.Notes = request.Notes;

        await _db.SaveChangesAsync(ct);

        return new ProductIngredientDto(
            ingredient.Id, ingredient.ProductId, ingredient.Name,
            ingredient.Quantity, ingredient.Unit, ingredient.IsAllergen, ingredient.AllergenType,
            ingredient.DisplayOrder, ingredient.Notes);
    }

    public async Task<bool> DeleteIngredientAsync(Guid productId, Guid ingredientId, CancellationToken ct = default)
    {
        var ingredient = await _db.ProductIngredients
            .FirstOrDefaultAsync(i => i.Id == ingredientId && i.ProductId == productId, ct);
        if (ingredient is null) return false;

        _db.ProductIngredients.Remove(ingredient);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
