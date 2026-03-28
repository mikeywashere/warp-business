namespace WarpBusiness.Shared.Catalog;

public record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    string? Slug,
    Guid? ParentCategoryId,
    string? ParentCategoryName,
    string? ImageUrl,
    int DisplayOrder,
    bool IsActive,
    int ProductCount,
    int SubCategoryCount,
    DateTimeOffset CreatedAt);

public record CategoryDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string? Slug,
    Guid? ParentCategoryId,
    string? ParentCategoryName,
    string? ImageUrl,
    int DisplayOrder,
    bool IsActive,
    int ProductCount,
    IReadOnlyList<CategoryDto> SubCategories,
    DateTimeOffset CreatedAt);

public record CreateCategoryRequest(
    string Name,
    string? Description,
    string? Slug,
    Guid? ParentCategoryId,
    string? ImageUrl,
    int DisplayOrder = 0,
    bool IsActive = true);

public record UpdateCategoryRequest(
    string Name,
    string? Description,
    string? Slug,
    Guid? ParentCategoryId,
    string? ImageUrl,
    int DisplayOrder,
    bool IsActive);
