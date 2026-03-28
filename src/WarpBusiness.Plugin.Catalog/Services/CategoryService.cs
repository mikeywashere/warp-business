using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Catalog.Data;
using WarpBusiness.Plugin.Catalog.Domain;
using WarpBusiness.Shared.Catalog;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Catalog.Services;

public class CategoryService : ICategoryService
{
    private readonly CatalogDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CategoryService(CatalogDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResult<CategoryDto>> GetCategoriesAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoryDto(
                c.Id, c.Name, c.Description, c.Slug,
                c.ParentCategoryId,
                c.ParentCategory != null ? c.ParentCategory.Name : null,
                c.ImageUrl, c.DisplayOrder, c.IsActive,
                c.Products.Count, c.SubCategories.Count,
                c.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<CategoryDto>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllCategoriesAsync(CancellationToken ct = default)
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id, c.Name, c.Description, c.Slug,
                c.ParentCategoryId,
                c.ParentCategory != null ? c.ParentCategory.Name : null,
                c.ImageUrl, c.DisplayOrder, c.IsActive,
                c.Products.Count, c.SubCategories.Count,
                c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<CategoryDetailDto?> GetCategoryDetailAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _db.Categories
            .AsNoTracking()
            .Include(c => c.SubCategories)
            .Include(c => c.ParentCategory)
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(ct);

        if (category is null) return null;

        var productCount = await _db.Products.CountAsync(p => p.CategoryId == id, ct);

        var subCategories = category.SubCategories
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id, c.Name, c.Description, c.Slug,
                c.ParentCategoryId, category.Name,
                c.ImageUrl, c.DisplayOrder, c.IsActive,
                0, 0, c.CreatedAt))
            .ToList();

        return new CategoryDetailDto(
            category.Id, category.Name, category.Description, category.Slug,
            category.ParentCategoryId,
            category.ParentCategory?.Name,
            category.ImageUrl, category.DisplayOrder, category.IsActive,
            productCount, subCategories,
            category.CreatedAt);
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, string userId, CancellationToken ct = default)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            Name = request.Name,
            Description = request.Description,
            Slug = request.Slug,
            ParentCategoryId = request.ParentCategoryId,
            ImageUrl = request.ImageUrl,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);

        return new CategoryDto(
            category.Id, category.Name, category.Description, category.Slug,
            category.ParentCategoryId, null,
            category.ImageUrl, category.DisplayOrder, category.IsActive,
            0, 0, category.CreatedAt);
    }

    public async Task<CategoryDto?> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _db.Categories.FindAsync([id], ct);
        if (category is null) return null;

        category.Name = request.Name;
        category.Description = request.Description;
        category.Slug = request.Slug;
        category.ParentCategoryId = request.ParentCategoryId;
        category.ImageUrl = request.ImageUrl;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new CategoryDto(
            category.Id, category.Name, category.Description, category.Slug,
            category.ParentCategoryId, null,
            category.ImageUrl, category.DisplayOrder, category.IsActive,
            0, 0, category.CreatedAt);
    }

    public async Task<DeleteCategoryResult> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _db.Categories
            .Include(c => c.Products)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (category is null) return DeleteCategoryResult.NotFound;
        if (category.Products.Any()) return DeleteCategoryResult.HasProducts;
        if (category.SubCategories.Any()) return DeleteCategoryResult.HasSubCategories;

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
        return DeleteCategoryResult.Deleted;
    }
}
