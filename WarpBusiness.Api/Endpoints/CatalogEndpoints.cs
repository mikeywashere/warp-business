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

    // ── Products ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetProducts(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var rawProducts = await db.Products
            .Where(p => p.TenantId == tenantId.Value)
            .Include(p => p.Category)
            .Include(p => p.Notations).ThenInclude(pn => pn.Notation)
            .Include(p => p.Media)
            .Include(p => p.Variants)
            .OrderBy(p => p.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return Results.Ok(rawProducts.Select(p => MapProductResponse(p, tenantId.Value)).ToList());
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
            .Include(p => p.Category)
            .Include(p => p.Notations).ThenInclude(pn => pn.Notation)
            .Include(p => p.Media)
            .Include(p => p.Variants)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        return product is null ? Results.NotFound() : Results.Ok(MapProductResponse(product, tenantId.Value));
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

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await db.Categories.AnyAsync(
                c => c.Id == request.CategoryId.Value && c.TenantId == tenantId.Value && c.IsActive,
                cancellationToken);
            if (!categoryExists)
                return Results.BadRequest(new { message = "Category not found in this tenant." });
        }

        if (!string.IsNullOrWhiteSpace(request.SKU) &&
            await db.Products.AnyAsync(p => p.SKU == request.SKU && p.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "A product with this SKU already exists." });

        if (request.NotationIds is { Count: > 0 })
        {
            foreach (var nid in request.NotationIds)
            {
                var notationExists = await db.Notations.AnyAsync(
                    n => n.Id == nid && n.TenantId == tenantId.Value, cancellationToken);
                if (!notationExists)
                    return Results.BadRequest(new { message = $"Notation {nid} not found in this tenant." });
            }
        }

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

        if (request.NotationIds is { Count: > 0 })
        {
            foreach (var nid in request.NotationIds)
                db.ProductNotations.Add(new ProductNotation { ProductId = product.Id, NotationId = nid });
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Created(
            $"/api/catalog/products/{product.Id}",
            new ProductResponse(product.Id, product.TenantId, product.CategoryId, null,
                product.Name, product.Description, product.Brand, product.SKU,
                product.BasePrice, product.Currency, product.IsActive,
                product.CreatedAt, product.UpdatedAt, 0, null, []));
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

        if (request.NotationIds is { Count: > 0 })
        {
            foreach (var nid in request.NotationIds)
            {
                var notationExists = await db.Notations.AnyAsync(
                    n => n.Id == nid && n.TenantId == tenantId.Value, cancellationToken);
                if (!notationExists)
                    return Results.BadRequest(new { message = $"Notation {nid} not found in this tenant." });
            }
        }

        product.CategoryId = request.CategoryId;
        product.Name = request.Name;
        product.Description = request.Description;
        product.Brand = request.Brand;
        product.SKU = newSku;
        product.BasePrice = request.BasePrice;
        product.Currency = request.Currency;
        product.IsActive = request.IsActive ?? product.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        // Replace notations
        var existingNotations = await db.ProductNotations
            .Where(pn => pn.ProductId == id)
            .ToListAsync(cancellationToken);
        db.ProductNotations.RemoveRange(existingNotations);

        if (request.NotationIds is { Count: > 0 })
        {
            foreach (var nid in request.NotationIds)
                db.ProductNotations.Add(new ProductNotation { ProductId = id, NotationId = nid });
        }

        await db.SaveChangesAsync(cancellationToken);

        var variantCount = await db.ProductVariants.CountAsync(v => v.ProductId == id, cancellationToken);
        var categoryName = product.CategoryId.HasValue
            ? await db.Categories.Where(c => c.Id == product.CategoryId.Value).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var thumbnailKey = await db.ProductMedia
            .Where(m => m.ProductId == id && m.TenantId == tenantId.Value && m.MediaType == MediaType.Image)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var notations = await db.ProductNotations
            .Where(pn => pn.ProductId == id)
            .Include(pn => pn.Notation)
            .Select(pn => new ProductNotationResponse(pn.NotationId, pn.Notation.Name, pn.Notation.Description, pn.Notation.Icon))
            .ToListAsync(cancellationToken);

        return Results.Ok(new ProductResponse(product.Id, product.TenantId, product.CategoryId, categoryName,
            product.Name, product.Description, product.Brand, product.SKU,
            product.BasePrice, product.Currency, product.IsActive,
            product.CreatedAt, product.UpdatedAt, variantCount, thumbnailKey,
            notations));
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
            .Include(v => v.OptionValues).ThenInclude(ov => ov.Option)
            .Include(v => v.OptionValues).ThenInclude(ov => ov.OptionValue)
            .Include(v => v.Media)
            .OrderBy(v => v.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return Results.Ok(variants.Select(MapVariantResponse).ToList());
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
            .Include(v => v.OptionValues).ThenInclude(ov => ov.Option)
            .Include(v => v.OptionValues).ThenInclude(ov => ov.OptionValue)
            .Include(v => v.Media)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        return variant is null ? Results.NotFound() : Results.Ok(MapVariantResponse(variant));
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

        var newSku = string.IsNullOrWhiteSpace(request.SKU) ? null : request.SKU;
        if (newSku is not null &&
            await db.ProductVariants.AnyAsync(v => v.SKU == newSku && v.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "A variant with this SKU already exists in this tenant." });

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            TenantId = tenantId.Value,
            SKU = newSku,
            Price = request.Price,
            PriceAdjustmentType = request.PriceAdjustmentType,
            StockQuantity = request.StockQuantity,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(cancellationToken);

        // Reload with navigation for response
        var created = await db.ProductVariants
            .Where(v => v.Id == variant.Id)
            .Include(v => v.OptionValues).ThenInclude(ov => ov.Option)
            .Include(v => v.OptionValues).ThenInclude(ov => ov.OptionValue)
            .Include(v => v.Media)
            .AsSplitQuery()
            .FirstAsync(cancellationToken);

        return Results.Created(
            $"/api/catalog/products/{productId}/variants/{variant.Id}",
            MapVariantResponse(created));
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

        var newSku = string.IsNullOrWhiteSpace(request.SKU) ? null : request.SKU;
        if (newSku is not null && newSku != variant.SKU &&
            await db.ProductVariants.AnyAsync(v => v.SKU == newSku && v.TenantId == tenantId.Value && v.Id != variantId, cancellationToken))
            return Results.Conflict(new { message = "A variant with this SKU already exists in this tenant." });

        variant.SKU = newSku;
        variant.Price = request.Price;
        variant.PriceAdjustmentType = request.PriceAdjustmentType ?? variant.PriceAdjustmentType;
        variant.StockQuantity = request.StockQuantity ?? variant.StockQuantity;
        variant.IsActive = request.IsActive ?? variant.IsActive;
        variant.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        // Reload with navigation for response
        var updated = await db.ProductVariants
            .Where(v => v.Id == variantId)
            .Include(v => v.OptionValues).ThenInclude(ov => ov.Option)
            .Include(v => v.OptionValues).ThenInclude(ov => ov.OptionValue)
            .Include(v => v.Media)
            .AsSplitQuery()
            .FirstAsync(cancellationToken);

        return Results.Ok(MapVariantResponse(updated));
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

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static ProductResponse MapProductResponse(Product p, Guid tenantId) => new(
        p.Id, p.TenantId, p.CategoryId,
        p.Category?.Name,
        p.Name, p.Description, p.Brand, p.SKU,
        p.BasePrice, p.Currency, p.IsActive,
        p.CreatedAt, p.UpdatedAt,
        p.Variants.Count(v => v.TenantId == tenantId),
        p.Media.Where(m => m.MediaType == MediaType.Image)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt)
            .Select(m => (Guid?)m.Id).FirstOrDefault(),
        p.Notations.Select(pn => new ProductNotationResponse(pn.NotationId, pn.Notation.Name, pn.Notation.Description, pn.Notation.Icon)).ToList());

    private static ProductVariantResponse MapVariantResponse(ProductVariant v) => new(
        v.Id, v.ProductId, v.TenantId,
        v.OptionValues
            .OrderBy(ov => ov.Option.SortOrder)
            .ThenBy(ov => ov.Option.Name)
            .Select(ov => new VariantOptionValueResponse(
                ov.VariantId,
                ov.Option.Name,
                ov.OptionValueId,
                ov.OptionValue.Value,
                ov.OptionValue.HexCode))
            .ToList(),
        v.SKU, v.Price, v.PriceAdjustmentType, v.StockQuantity, v.IsActive,
        v.CreatedAt, v.UpdatedAt,
        v.Media.Where(m => m.MediaType == MediaType.Image)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt)
            .Select(m => (Guid?)m.Id).FirstOrDefault());
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

public record ProductResponse(
    Guid Id, Guid TenantId, Guid? CategoryId, string? CategoryName,
    string Name, string? Description, string? Brand, string? SKU,
    decimal BasePrice, string Currency, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int VariantCount,
    Guid? ThumbnailMediaId,
    List<ProductNotationResponse> Notations);

public record CreateProductRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    string? Description = null,
    string? Brand = null,
    string? SKU = null,
    Guid? CategoryId = null,
    List<Guid>? NotationIds = null);

public record UpdateProductRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    string? Description = null,
    string? Brand = null,
    string? SKU = null,
    Guid? CategoryId = null,
    bool? IsActive = null,
    List<Guid>? NotationIds = null);

public record ProductVariantResponse(
    Guid Id, Guid ProductId, Guid TenantId,
    List<VariantOptionValueResponse> OptionValues,
    string? SKU, decimal? Price, PriceAdjustmentType PriceAdjustmentType,
    int StockQuantity, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    Guid? ThumbnailMediaId);

public record CreateProductVariantRequest(
    string? SKU = null,
    decimal? Price = null,
    PriceAdjustmentType PriceAdjustmentType = PriceAdjustmentType.None,
    int StockQuantity = 0);

public record UpdateProductVariantRequest(
    string? SKU = null,
    decimal? Price = null,
    PriceAdjustmentType? PriceAdjustmentType = null,
    int? StockQuantity = null,
    bool? IsActive = null);

public record VariantOptionValueResponse(
    Guid VariantId,
    string OptionName,
    Guid OptionValueId,
    string Value,
    string? HexCode = null);

public record ProductNotationResponse(
    Guid NotationId,
    string Name,
    string? Description,
    NotationIcon? Icon);
