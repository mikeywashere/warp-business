namespace WarpBusiness.Shared.Catalog;

public record ProductImageDto(
    Guid Id,
    Guid ProductId,
    Guid? ProductVariantId,
    string Url,
    string? FileName,
    string? AltText,
    string? ContentType,
    long? FileSizeBytes,
    bool IsPrimary,
    int DisplayOrder,
    DateTimeOffset CreatedAt);

public record CreateProductImageRequest(
    string Url,
    string? FileName,
    string? AltText,
    string? ContentType,
    long? FileSizeBytes,
    Guid? ProductVariantId = null,
    bool IsPrimary = false,
    int DisplayOrder = 0);
