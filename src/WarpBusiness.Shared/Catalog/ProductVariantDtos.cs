namespace WarpBusiness.Shared.Catalog;

public record ProductOptionDto(
    Guid Id,
    string Name,
    int DisplayOrder,
    IReadOnlyList<ProductOptionValueDto> Values);

public record ProductOptionValueDto(
    Guid Id,
    string Value,
    int DisplayOrder);

public record CreateProductOptionRequest(
    string Name,
    int DisplayOrder = 0,
    IReadOnlyList<string>? Values = null);

public record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    string? Sku,
    string? Barcode,
    decimal? Price,
    decimal? CostPrice,
    decimal? Weight,
    int StockQuantity,
    int? LowStockThreshold,
    bool TrackInventory,
    bool IsActive,
    int DisplayOrder,
    IReadOnlyList<VariantOptionValueDto> OptionValues,
    DateTimeOffset CreatedAt);

public record VariantOptionValueDto(
    string OptionName,
    string Value);

public record CreateProductVariantRequest(
    string? Sku = null,
    string? Barcode = null,
    decimal? Price = null,
    decimal? CostPrice = null,
    decimal? Weight = null,
    int StockQuantity = 0,
    int? LowStockThreshold = null,
    bool TrackInventory = false,
    bool IsActive = true,
    int DisplayOrder = 0,
    IReadOnlyList<Guid>? OptionValueIds = null);

public record UpdateProductVariantRequest(
    string? Sku,
    string? Barcode,
    decimal? Price,
    decimal? CostPrice,
    decimal? Weight,
    int StockQuantity,
    int? LowStockThreshold,
    bool TrackInventory,
    bool IsActive,
    int DisplayOrder);
