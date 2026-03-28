namespace WarpBusiness.Shared.Catalog;

public record ProductIngredientDto(
    Guid Id,
    Guid ProductId,
    string Name,
    string? Quantity,
    string? Unit,
    bool IsAllergen,
    string? AllergenType,
    int DisplayOrder,
    string? Notes);

public record CreateProductIngredientRequest(
    string Name,
    string? Quantity = null,
    string? Unit = null,
    bool IsAllergen = false,
    string? AllergenType = null,
    int DisplayOrder = 0,
    string? Notes = null);

public record UpdateProductIngredientRequest(
    string Name,
    string? Quantity,
    string? Unit,
    bool IsAllergen,
    string? AllergenType,
    int DisplayOrder,
    string? Notes);
