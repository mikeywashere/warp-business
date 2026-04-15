using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        // Categories
        var categories = app.MapGroup("/api/catalog/categories").RequireAuthorization();
        categories.MapGet("", GetCategories).WithName("GetCatalogCategories");
        categories.MapGet("{id:guid}", GetCategory).WithName("GetCatalogCategory");
        categories.MapPost("", CreateCategory).WithName("CreateCatalogCategory");
        categories.MapPut("{id:guid}", UpdateCategory).WithName("UpdateCatalogCategory");
        categories.MapDelete("{id:guid}", DeleteCategory).WithName("DeleteCatalogCategory");

        // Colors
        var colors = app.MapGroup("/api/catalog/colors").RequireAuthorization();
        colors.MapGet("", GetColors).WithName("GetCatalogColors");
        colors.MapGet("{id:guid}", GetColor).WithName("GetCatalogColor");
        colors.MapPost("", CreateColor).WithName("CreateCatalogColor");
        colors.MapPut("{id:guid}", UpdateColor).WithName("UpdateCatalogColor");
        colors.MapDelete("{id:guid}", DeleteColor).WithName("DeleteCatalogColor");

        // Sizes
        var sizes = app.MapGroup("/api/catalog/sizes").RequireAuthorization();
        sizes.MapGet("", GetSizes).WithName("GetCatalogSizes");
        sizes.MapGet("{id:guid}", GetSize).WithName("GetCatalogSize");
        sizes.MapPost("", CreateSize).WithName("CreateCatalogSize");
        sizes.MapPut("{id:guid}", UpdateSize).WithName("UpdateCatalogSize");
        sizes.MapDelete("{id:guid}", DeleteSize).WithName("DeleteCatalogSize");

        // Products
        var products = app.MapGroup("/api/catalog/products").RequireAuthorization();
        products.MapGet("", GetProducts).WithName("GetCatalogProducts");
        products.MapGet("{id:guid}", GetProduct).WithName("GetCatalogProduct");
        products.MapPost("", CreateProduct).WithName("CreateCatalogProduct");
        products.MapPut("{id:guid}", UpdateProduct).WithName("UpdateCatalogProduct");
        products.MapDelete("{id:guid}", DeleteProduct).WithName("DeleteCatalogProduct");

        // Product Variants
        var variants = app.MapGroup("/api/catalog/products/{productId:guid}/variants").RequireAuthorization();
        variants.MapGet("", GetVariants).WithName("GetProductVariants");
        variants.MapGet("{variantId:guid}", GetVariant).WithName("GetProductVariant");
        variants.MapPost("", CreateVariant).WithName("CreateProductVariant");
        variants.MapPut("{variantId:guid}", UpdateVariant).WithName("UpdateProductVariant");
        variants.MapDelete("{variantId:guid}", DeleteVariant).WithName("DeleteProductVariant");
    }

    // ── Categories ────────────────────────────────────────────────────────────

    private static async Task<IResult> GetCategories(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var categories = await db.Categories
            .Where(c => c.TenantId == tenantId.Value)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(
                c.Id, c.TenantId, c.ParentCategoryId,
                c.Name, c.Description, c.IsActive,
                c.CreatedAt, c.UpdatedAt,
                c.SubCategories.Count(sc => sc.TenantId == tenantId.Value),
                c.Products.Count(p => p.TenantId == tenantId.Value)))
            .ToListAsync(cancellationToken);

        return Results.Ok(categories);
    }

    private static async Task<IResult> GetCategory(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var category = await db.Categories
            .Where(c => c.Id == id && c.TenantId == tenantId.Value)
            .Select(c => new CategoryResponse(
                c.Id, c.TenantId, c.ParentCategoryId,
                c.Name, c.Description, c.IsActive,
                c.CreatedAt, c.UpdatedAt,
                c.SubCategories.Count(sc => sc.TenantId == tenantId.Value),
                c.Products.Count(p => p.TenantId == tenantId.Value)))
            .FirstOrDefaultAsync(cancellationToken);

        return category is null ? Results.NotFound() : Results.Ok(category);
    }

    private static async Task<IResult> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Category name is required." });

        // Validate parent belongs to this tenant
        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await db.Categories.AnyAsync(
                c => c.Id == request.ParentCategoryId.Value && c.TenantId == tenantId.Value,
                cancellationToken);
            if (!parentExists)
                return Results.BadRequest(new { message = "Parent category not found in this tenant." });
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ParentCategoryId = request.ParentCategoryId,
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Categories.Add(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A category with this name already exists at this level." });
        }

        return Results.Created(
            $"/api/catalog/categories/{category.Id}",
            new CategoryResponse(category.Id, category.TenantId, category.ParentCategoryId,
                category.Name, category.Description, category.IsActive,
                category.CreatedAt, category.UpdatedAt, 0, 0));
    }

    private static async Task<IResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);
        if (category is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Category name is required." });

        // Validate new parent belongs to this tenant and won't create a cycle
        if (request.ParentCategoryId.HasValue && request.ParentCategoryId != category.ParentCategoryId)
        {
            var parentExists = await db.Categories.AnyAsync(
                c => c.Id == request.ParentCategoryId.Value && c.TenantId == tenantId.Value,
                cancellationToken);
            if (!parentExists)
                return Results.BadRequest(new { message = "Parent category not found in this tenant." });

            if (await WouldCreateCycleAsync(db, id, request.ParentCategoryId.Value, cancellationToken))
                return Results.BadRequest(new { message = "Setting this parent would create a circular category hierarchy." });
        }

        category.ParentCategoryId = request.ParentCategoryId;
        category.Name = request.Name;
        category.Description = request.Description;
        category.IsActive = request.IsActive ?? category.IsActive;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A category with this name already exists at this level." });
        }

        var subCount = await db.Categories.CountAsync(c => c.ParentCategoryId == id, cancellationToken);
        var productCount = await db.Products.CountAsync(p => p.CategoryId == id, cancellationToken);

        return Results.Ok(new CategoryResponse(category.Id, category.TenantId, category.ParentCategoryId,
            category.Name, category.Description, category.IsActive,
            category.CreatedAt, category.UpdatedAt, subCount, productCount));
    }

    private static async Task<IResult> DeleteCategory(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);
        if (category is null)
            return Results.NotFound();

        var hasChildren = await db.Categories.AnyAsync(c => c.ParentCategoryId == id, cancellationToken);
        if (hasChildren)
            return Results.Conflict(new { message = "Cannot delete a category that has sub-categories." });

        var hasProducts = await db.Products.AnyAsync(p => p.CategoryId == id, cancellationToken);
        if (hasProducts)
        {
            // Soft delete instead
            category.IsActive = false;
            category.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Category has linked products and was deactivated instead of deleted." });
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<bool> WouldCreateCycleAsync(
        CatalogDbContext db, Guid categoryId, Guid newParentId, CancellationToken cancellationToken)
    {
        // Walk up the new parent's ancestors; if we encounter categoryId it would be a cycle
        var current = newParentId;
        while (true)
        {
            var parent = await db.Categories
                .Where(c => c.Id == current)
                .Select(c => (Guid?)c.ParentCategoryId)
                .FirstOrDefaultAsync(cancellationToken);

            if (parent is null) return false;
            if (parent.Value == categoryId) return true;
            current = parent.Value;
        }
    }

    // ── Colors ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetColors(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var colors = await db.Colors
            .Where(c => c.TenantId == tenantId.Value)
            .OrderBy(c => c.Name)
            .Select(c => new ColorResponse(c.Id, c.TenantId, c.Name, c.HexCode, c.IsActive, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(colors);
    }

    private static async Task<IResult> GetColor(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var color = await db.Colors
            .Where(c => c.Id == id && c.TenantId == tenantId.Value)
            .Select(c => new ColorResponse(c.Id, c.TenantId, c.Name, c.HexCode, c.IsActive, c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return color is null ? Results.NotFound() : Results.Ok(color);
    }

    private static async Task<IResult> CreateColor(
        [FromBody] CreateColorRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Color name is required." });

        if (await db.Colors.AnyAsync(c => c.Name == request.Name && c.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "A color with this name already exists." });

        var color = new Color
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            HexCode = request.HexCode,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Colors.Add(color);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/catalog/colors/{color.Id}",
            new ColorResponse(color.Id, color.TenantId, color.Name, color.HexCode, color.IsActive, color.CreatedAt, color.UpdatedAt));
    }

    private static async Task<IResult> UpdateColor(
        Guid id,
        [FromBody] UpdateColorRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var color = await db.Colors
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);
        if (color is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Color name is required." });

        if (!string.Equals(color.Name, request.Name, StringComparison.OrdinalIgnoreCase) &&
            await db.Colors.AnyAsync(c => c.Name == request.Name && c.TenantId == tenantId.Value && c.Id != id, cancellationToken))
            return Results.Conflict(new { message = "A color with this name already exists." });

        color.Name = request.Name;
        color.HexCode = request.HexCode;
        color.IsActive = request.IsActive ?? color.IsActive;
        color.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ColorResponse(color.Id, color.TenantId, color.Name, color.HexCode, color.IsActive, color.CreatedAt, color.UpdatedAt));
    }

    private static async Task<IResult> DeleteColor(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var color = await db.Colors
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);
        if (color is null)
            return Results.NotFound();

        var inUse = await db.ProductVariants.AnyAsync(v => v.ColorId == id, cancellationToken);
        if (inUse)
        {
            color.IsActive = false;
            color.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Color is used by product variants and was deactivated instead of deleted." });
        }

        db.Colors.Remove(color);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // ── Sizes ─────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSizes(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var sizes = await db.Sizes
            .Where(s => s.TenantId == tenantId.Value)
            .OrderBy(s => s.SizeType)
            .ThenBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .Select(s => new SizeResponse(s.Id, s.TenantId, s.Name, s.SizeType, s.SortOrder, s.IsActive, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(sizes);
    }

    private static async Task<IResult> GetSize(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var size = await db.Sizes
            .Where(s => s.Id == id && s.TenantId == tenantId.Value)
            .Select(s => new SizeResponse(s.Id, s.TenantId, s.Name, s.SizeType, s.SortOrder, s.IsActive, s.CreatedAt, s.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return size is null ? Results.NotFound() : Results.Ok(size);
    }

    private static async Task<IResult> CreateSize(
        [FromBody] CreateSizeRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Size name is required." });

        if (await db.Sizes.AnyAsync(s => s.Name == request.Name && s.SizeType == request.SizeType && s.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "A size with this name already exists in this size type." });

        var size = new Size
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            SizeType = request.SizeType ?? "General",
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Sizes.Add(size);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/catalog/sizes/{size.Id}",
            new SizeResponse(size.Id, size.TenantId, size.Name, size.SizeType, size.SortOrder, size.IsActive, size.CreatedAt, size.UpdatedAt));
    }

    private static async Task<IResult> UpdateSize(
        Guid id,
        [FromBody] UpdateSizeRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var size = await db.Sizes
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId.Value, cancellationToken);
        if (size is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Size name is required." });

        var newSizeType = request.SizeType ?? size.SizeType;
        if ((!string.Equals(size.Name, request.Name, StringComparison.OrdinalIgnoreCase) || size.SizeType != newSizeType) &&
            await db.Sizes.AnyAsync(s => s.Name == request.Name && s.SizeType == newSizeType && s.TenantId == tenantId.Value && s.Id != id, cancellationToken))
            return Results.Conflict(new { message = "A size with this name already exists in this size type." });

        size.Name = request.Name;
        size.SizeType = newSizeType;
        size.SortOrder = request.SortOrder ?? size.SortOrder;
        size.IsActive = request.IsActive ?? size.IsActive;
        size.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new SizeResponse(size.Id, size.TenantId, size.Name, size.SizeType, size.SortOrder, size.IsActive, size.CreatedAt, size.UpdatedAt));
    }

    private static async Task<IResult> DeleteSize(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var size = await db.Sizes
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId.Value, cancellationToken);
        if (size is null)
            return Results.NotFound();

        var inUse = await db.ProductVariants.AnyAsync(v => v.SizeId == id, cancellationToken);
        if (inUse)
        {
            size.IsActive = false;
            size.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Size is used by product variants and was deactivated instead of deleted." });
        }

        db.Sizes.Remove(size);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // ── Products ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetProducts(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var products = await db.Products
            .Where(p => p.TenantId == tenantId.Value)
            .OrderBy(p => p.Name)
            .Select(p => new ProductResponse(
                p.Id, p.TenantId, p.CategoryId,
                p.Category != null ? p.Category.Name : null,
                p.Name, p.Description, p.Brand, p.SKU,
                p.BasePrice, p.Currency, p.IsActive,
                p.CreatedAt, p.UpdatedAt,
                p.Variants.Count(v => v.TenantId == tenantId.Value),
                p.Media.Where(m => m.MediaType == MediaType.Image).OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt).Select(m => m.ObjectKey).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return Results.Ok(products);
    }

    private static async Task<IResult> GetProduct(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var product = await db.Products
            .Where(p => p.Id == id && p.TenantId == tenantId.Value)
            .Select(p => new ProductResponse(
                p.Id, p.TenantId, p.CategoryId,
                p.Category != null ? p.Category.Name : null,
                p.Name, p.Description, p.Brand, p.SKU,
                p.BasePrice, p.Currency, p.IsActive,
                p.CreatedAt, p.UpdatedAt,
                p.Variants.Count(v => v.TenantId == tenantId.Value),
                p.Media.Where(m => m.MediaType == MediaType.Image).OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt).Select(m => m.ObjectKey).FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Product name is required." });

        if (string.IsNullOrWhiteSpace(request.Currency))
            return Results.BadRequest(new { message = "Currency is required." });

        // Validate category belongs to this tenant
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await db.Categories.AnyAsync(
                c => c.Id == request.CategoryId.Value && c.TenantId == tenantId.Value && c.IsActive,
                cancellationToken);
            if (!categoryExists)
                return Results.BadRequest(new { message = "Category not found in this tenant." });
        }

        // Validate SKU uniqueness within tenant
        if (!string.IsNullOrWhiteSpace(request.SKU) &&
            await db.Products.AnyAsync(p => p.SKU == request.SKU && p.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "A product with this SKU already exists." });

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            CategoryId = request.CategoryId,
            Name = request.Name,
            Description = request.Description,
            Brand = request.Brand,
            SKU = string.IsNullOrWhiteSpace(request.SKU) ? null : request.SKU,
            BasePrice = request.BasePrice,
            Currency = request.Currency,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/catalog/products/{product.Id}",
            new ProductResponse(product.Id, product.TenantId, product.CategoryId, null,
                product.Name, product.Description, product.Brand, product.SKU,
                product.BasePrice, product.Currency, product.IsActive,
                product.CreatedAt, product.UpdatedAt, 0, null));
    }

    private static async Task<IResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value, cancellationToken);
        if (product is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Product name is required." });

        if (string.IsNullOrWhiteSpace(request.Currency))
            return Results.BadRequest(new { message = "Currency is required." });

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await db.Categories.AnyAsync(
                c => c.Id == request.CategoryId.Value && c.TenantId == tenantId.Value && c.IsActive,
                cancellationToken);
            if (!categoryExists)
                return Results.BadRequest(new { message = "Category not found in this tenant." });
        }

        var newSku = string.IsNullOrWhiteSpace(request.SKU) ? null : request.SKU;
        if (newSku is not null && newSku != product.SKU &&
            await db.Products.AnyAsync(p => p.SKU == newSku && p.TenantId == tenantId.Value && p.Id != id, cancellationToken))
            return Results.Conflict(new { message = "A product with this SKU already exists." });

        product.CategoryId = request.CategoryId;
        product.Name = request.Name;
        product.Description = request.Description;
        product.Brand = request.Brand;
        product.SKU = newSku;
        product.BasePrice = request.BasePrice;
        product.Currency = request.Currency;
        product.IsActive = request.IsActive ?? product.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var variantCount = await db.ProductVariants.CountAsync(v => v.ProductId == id, cancellationToken);
        var categoryName = product.CategoryId.HasValue
            ? await db.Categories.Where(c => c.Id == product.CategoryId.Value).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var thumbnailKey = await db.ProductMedia
            .Where(m => m.ProductId == id && m.TenantId == tenantId.Value && m.MediaType == MediaType.Image)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt)
            .Select(m => m.ObjectKey)
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(new ProductResponse(product.Id, product.TenantId, product.CategoryId, categoryName,
            product.Name, product.Description, product.Brand, product.SKU,
            product.BasePrice, product.Currency, product.IsActive,
            product.CreatedAt, product.UpdatedAt, variantCount, thumbnailKey));
    }

    private static async Task<IResult> DeleteProduct(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value, cancellationToken);
        if (product is null)
            return Results.NotFound();

        // Variants cascade delete via FK
        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // ── Product Variants ──────────────────────────────────────────────────────

    private static async Task<IResult> GetVariants(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productExists = await db.Products.AnyAsync(
            p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (!productExists)
            return Results.NotFound();

        var variants = await db.ProductVariants
            .Where(v => v.ProductId == productId && v.TenantId == tenantId.Value)
            .OrderBy(v => v.Color != null ? v.Color.Name : "")
            .ThenBy(v => v.Size != null ? v.Size.SortOrder : 0)
            .Select(v => new ProductVariantResponse(
                v.Id, v.ProductId, v.TenantId,
                v.ColorId, v.Color != null ? v.Color.Name : null, v.Color != null ? v.Color.HexCode : null,
                v.SizeId, v.Size != null ? v.Size.Name : null, v.Size != null ? v.Size.SizeType : null,
                v.SKU, v.Price, v.StockQuantity, v.IsActive, v.CreatedAt, v.UpdatedAt,
                v.Media.Where(m => m.MediaType == MediaType.Image).OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt).Select(m => m.ObjectKey).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return Results.Ok(variants);
    }

    private static async Task<IResult> GetVariant(
        Guid productId,
        Guid variantId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var variant = await db.ProductVariants
            .Where(v => v.Id == variantId && v.ProductId == productId && v.TenantId == tenantId.Value)
            .Select(v => new ProductVariantResponse(
                v.Id, v.ProductId, v.TenantId,
                v.ColorId, v.Color != null ? v.Color.Name : null, v.Color != null ? v.Color.HexCode : null,
                v.SizeId, v.Size != null ? v.Size.Name : null, v.Size != null ? v.Size.SizeType : null,
                v.SKU, v.Price, v.StockQuantity, v.IsActive, v.CreatedAt, v.UpdatedAt,
                v.Media.Where(m => m.MediaType == MediaType.Image).OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt).Select(m => m.ObjectKey).FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

        return variant is null ? Results.NotFound() : Results.Ok(variant);
    }

    private static async Task<IResult> CreateVariant(
        Guid productId,
        [FromBody] CreateProductVariantRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (product is null)
            return Results.NotFound();

        // Validate color and size belong to this tenant
        if (request.ColorId.HasValue)
        {
            var colorExists = await db.Colors.AnyAsync(
                c => c.Id == request.ColorId.Value && c.TenantId == tenantId.Value, cancellationToken);
            if (!colorExists)
                return Results.BadRequest(new { message = "Color not found in this tenant." });
        }

        if (request.SizeId.HasValue)
        {
            var sizeExists = await db.Sizes.AnyAsync(
                s => s.Id == request.SizeId.Value && s.TenantId == tenantId.Value, cancellationToken);
            if (!sizeExists)
                return Results.BadRequest(new { message = "Size not found in this tenant." });
        }

        var newSku = string.IsNullOrWhiteSpace(request.SKU) ? null : request.SKU;
        if (newSku is not null &&
            await db.ProductVariants.AnyAsync(v => v.SKU == newSku && v.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "A variant with this SKU already exists in this tenant." });

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            TenantId = tenantId.Value,
            ColorId = request.ColorId,
            SizeId = request.SizeId,
            SKU = newSku,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.ProductVariants.Add(variant);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A variant with this color/size combination already exists for this product." });
        }

        return Results.Created(
            $"/api/catalog/products/{productId}/variants/{variant.Id}",
            new ProductVariantResponse(
                variant.Id, variant.ProductId, variant.TenantId,
                variant.ColorId, null, null,
                variant.SizeId, null, null,
                variant.SKU, variant.Price, variant.StockQuantity, variant.IsActive,
                variant.CreatedAt, variant.UpdatedAt, null));
    }

    private static async Task<IResult> UpdateVariant(
        Guid productId,
        Guid variantId,
        [FromBody] UpdateProductVariantRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var variant = await db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId && v.TenantId == tenantId.Value, cancellationToken);
        if (variant is null)
            return Results.NotFound();

        if (request.ColorId.HasValue)
        {
            var colorExists = await db.Colors.AnyAsync(
                c => c.Id == request.ColorId.Value && c.TenantId == tenantId.Value, cancellationToken);
            if (!colorExists)
                return Results.BadRequest(new { message = "Color not found in this tenant." });
        }

        if (request.SizeId.HasValue)
        {
            var sizeExists = await db.Sizes.AnyAsync(
                s => s.Id == request.SizeId.Value && s.TenantId == tenantId.Value, cancellationToken);
            if (!sizeExists)
                return Results.BadRequest(new { message = "Size not found in this tenant." });
        }

        var newSku = string.IsNullOrWhiteSpace(request.SKU) ? null : request.SKU;
        if (newSku is not null && newSku != variant.SKU &&
            await db.ProductVariants.AnyAsync(v => v.SKU == newSku && v.TenantId == tenantId.Value && v.Id != variantId, cancellationToken))
            return Results.Conflict(new { message = "A variant with this SKU already exists in this tenant." });

        variant.ColorId = request.ColorId;
        variant.SizeId = request.SizeId;
        variant.SKU = newSku;
        variant.Price = request.Price;
        variant.StockQuantity = request.StockQuantity ?? variant.StockQuantity;
        variant.IsActive = request.IsActive ?? variant.IsActive;
        variant.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A variant with this color/size combination already exists for this product." });
        }

        var colorName = variant.ColorId.HasValue
            ? await db.Colors.Where(c => c.Id == variant.ColorId.Value).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var sizeName = variant.SizeId.HasValue
            ? await db.Sizes.Where(s => s.Id == variant.SizeId.Value).Select(s => s.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var variantThumbnailKey = await db.ProductMedia
            .Where(m => m.VariantId == variantId && m.TenantId == tenantId.Value && m.MediaType == MediaType.Image)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt)
            .Select(m => m.ObjectKey)
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(new ProductVariantResponse(
            variant.Id, variant.ProductId, variant.TenantId,
            variant.ColorId, colorName, null,
            variant.SizeId, sizeName, null,
            variant.SKU, variant.Price, variant.StockQuantity, variant.IsActive,
            variant.CreatedAt, variant.UpdatedAt, variantThumbnailKey));
    }

    private static async Task<IResult> DeleteVariant(
        Guid productId,
        Guid variantId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var variant = await db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId && v.TenantId == tenantId.Value, cancellationToken);
        if (variant is null)
            return Results.NotFound();

        db.ProductVariants.Remove(variant);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CategoryResponse(
    Guid Id, Guid TenantId, Guid? ParentCategoryId,
    string Name, string? Description, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int SubCategoryCount, int ProductCount);

public record CreateCategoryRequest(
    string Name,
    string? Description = null,
    Guid? ParentCategoryId = null);

public record UpdateCategoryRequest(
    string Name,
    string? Description = null,
    Guid? ParentCategoryId = null,
    bool? IsActive = null);

public record ColorResponse(
    Guid Id, Guid TenantId,
    string Name, string? HexCode, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateColorRequest(string Name, string? HexCode = null);

public record UpdateColorRequest(string Name, string? HexCode = null, bool? IsActive = null);

public record SizeResponse(
    Guid Id, Guid TenantId,
    string Name, string SizeType, int SortOrder, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateSizeRequest(string Name, string? SizeType = null, int SortOrder = 0);

public record UpdateSizeRequest(string Name, string? SizeType = null, int? SortOrder = null, bool? IsActive = null);

public record ProductResponse(
    Guid Id, Guid TenantId, Guid? CategoryId, string? CategoryName,
    string Name, string? Description, string? Brand, string? SKU,
    decimal BasePrice, string Currency, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int VariantCount,
    string? ThumbnailKey);

public record CreateProductRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    string? Description = null,
    string? Brand = null,
    string? SKU = null,
    Guid? CategoryId = null);

public record UpdateProductRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    string? Description = null,
    string? Brand = null,
    string? SKU = null,
    Guid? CategoryId = null,
    bool? IsActive = null);

public record ProductVariantResponse(
    Guid Id, Guid ProductId, Guid TenantId,
    Guid? ColorId, string? ColorName, string? ColorHex,
    Guid? SizeId, string? SizeName, string? SizeType,
    string? SKU, decimal? Price, int StockQuantity, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    string? ThumbnailKey);

public record CreateProductVariantRequest(
    Guid? ColorId = null,
    Guid? SizeId = null,
    string? SKU = null,
    decimal? Price = null,
    int StockQuantity = 0);

public record UpdateProductVariantRequest(
    Guid? ColorId = null,
    Guid? SizeId = null,
    string? SKU = null,
    decimal? Price = null,
    int? StockQuantity = null,
    bool? IsActive = null);
