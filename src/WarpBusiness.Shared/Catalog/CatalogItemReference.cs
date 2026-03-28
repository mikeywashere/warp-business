namespace WarpBusiness.Shared.Catalog;

/// <summary>
/// Lightweight reference to a catalog product/variant for use by other plugins (e.g. Invoice).
/// Carries enough info to display a line item without querying the Catalog plugin.
/// </summary>
public record CatalogItemReference(
    Guid ProductId,
    string ProductName,
    string? ProductSku,
    Guid? VariantId,
    string? VariantSku,
    decimal UnitPrice,
    string Currency);
