using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Storage;

namespace WarpBusiness.Api.Endpoints;

public static class CatalogImageEndpoints
{
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB
    private const long MaxVideoSize = 500 * 1024 * 1024; // 500MB
    
    private static readonly string[] AllowedImageContentTypes = 
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    ];

    private static readonly string[] AllowedVideoContentTypes = 
    [
        "video/mp4",
        "video/webm",
        "video/quicktime",
        "video/x-msvideo"
    ];

    public static void MapCatalogImageEndpoints(this WebApplication app)
    {
        var images = app.MapGroup("/api/catalog");

        // Product image endpoints
        images.MapPost("/products/{productId:guid}/image", UploadProductImage)
            .WithName("UploadProductImage")
            .RequireAuthorization()
            .DisableAntiforgery();

        images.MapDelete("/products/{productId:guid}/image", DeleteProductImage)
            .WithName("DeleteProductImage")
            .RequireAuthorization();

        // Variant image endpoints
        images.MapPost("/variants/{variantId:guid}/image", UploadVariantImage)
            .WithName("UploadVariantImage")
            .RequireAuthorization()
            .DisableAntiforgery();

        images.MapDelete("/variants/{variantId:guid}/image", DeleteVariantImage)
            .WithName("DeleteVariantImage")
            .RequireAuthorization();

        // Proxy GET endpoint for image access
        images.MapGet("/images/{*objectKey}", GetImage)
            .WithName("GetCatalogImage")
            .AllowAnonymous();

        // Product video endpoints
        images.MapPost("/products/{productId:guid}/video", UploadProductVideo)
            .WithName("UploadProductVideo")
            .RequireAuthorization()
            .DisableAntiforgery();

        images.MapDelete("/products/{productId:guid}/video", DeleteProductVideo)
            .WithName("DeleteProductVideo")
            .RequireAuthorization();

        // Variant video endpoints
        images.MapPost("/variants/{variantId:guid}/video", UploadVariantVideo)
            .WithName("UploadVariantVideo")
            .RequireAuthorization()
            .DisableAntiforgery();

        images.MapDelete("/variants/{variantId:guid}/video", DeleteVariantVideo)
            .WithName("DeleteVariantVideo")
            .RequireAuthorization();

        // Proxy GET endpoint for video access
        images.MapGet("/videos/{*objectKey}", GetVideo)
            .WithName("GetCatalogVideo")
            .AllowAnonymous();
    }

    private static async Task<IResult> UploadProductImage(
        Guid productId,
        IFormFile image,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        // Validate the product belongs to the tenant
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (product is null)
            return Results.NotFound(new { message = "Product not found." });

        // Validate content type
        if (!AllowedImageContentTypes.Contains(image.ContentType.ToLowerInvariant()))
            return Results.BadRequest(new { message = $"Invalid content type. Allowed: {string.Join(", ", AllowedImageContentTypes)}" });

        // Validate size
        if (image.Length > MaxImageSize)
            return Results.BadRequest(new { message = $"Image size exceeds maximum allowed ({MaxImageSize / 1024 / 1024}MB)." });

        // Generate object key with tenant prefix
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            extension = image.ContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var objectKey = $"{tenantId}/products/{productId}/{Guid.NewGuid()}{extension}";

        // Ensure bucket exists
        await storage.EnsureBucketExistsAsync("warp-catalog", cancellationToken);

        // Delete old image if exists
        if (!string.IsNullOrEmpty(product.ImageKey))
        {
            await storage.DeleteAsync("warp-catalog", product.ImageKey, cancellationToken);
        }

        // Upload new image
        using var stream = image.OpenReadStream();
        await storage.UploadAsync("warp-catalog", objectKey, stream, image.ContentType, image.Length, cancellationToken);

        // Update product
        product.ImageKey = objectKey;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { imageKey = objectKey });
    }

    private static async Task<IResult> DeleteProductImage(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (product is null)
            return Results.NotFound(new { message = "Product not found." });

        if (string.IsNullOrEmpty(product.ImageKey))
            return Results.NotFound(new { message = "Product has no image." });

        // Delete from storage
        await storage.DeleteAsync("warp-catalog", product.ImageKey, cancellationToken);

        // Update product
        product.ImageKey = null;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UploadVariantImage(
        Guid variantId,
        IFormFile image,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        // Validate the variant belongs to the tenant
        var variant = await db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.TenantId == tenantId.Value, cancellationToken);
        if (variant is null)
            return Results.NotFound(new { message = "Product variant not found." });

        // Validate content type
        if (!AllowedImageContentTypes.Contains(image.ContentType.ToLowerInvariant()))
            return Results.BadRequest(new { message = $"Invalid content type. Allowed: {string.Join(", ", AllowedImageContentTypes)}" });

        // Validate size
        if (image.Length > MaxImageSize)
            return Results.BadRequest(new { message = $"Image size exceeds maximum allowed ({MaxImageSize / 1024 / 1024}MB)." });

        // Generate object key with tenant prefix
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            extension = image.ContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var objectKey = $"{tenantId}/variants/{variantId}/{Guid.NewGuid()}{extension}";

        // Ensure bucket exists
        await storage.EnsureBucketExistsAsync("warp-catalog", cancellationToken);

        // Delete old image if exists
        if (!string.IsNullOrEmpty(variant.ImageKey))
        {
            await storage.DeleteAsync("warp-catalog", variant.ImageKey, cancellationToken);
        }

        // Upload new image
        using var stream = image.OpenReadStream();
        await storage.UploadAsync("warp-catalog", objectKey, stream, image.ContentType, image.Length, cancellationToken);

        // Update variant
        variant.ImageKey = objectKey;
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { imageKey = objectKey });
    }

    private static async Task<IResult> DeleteVariantImage(
        Guid variantId,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var variant = await db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.TenantId == tenantId.Value, cancellationToken);
        if (variant is null)
            return Results.NotFound(new { message = "Product variant not found." });

        if (string.IsNullOrEmpty(variant.ImageKey))
            return Results.NotFound(new { message = "Product variant has no image." });

        // Delete from storage
        await storage.DeleteAsync("warp-catalog", variant.ImageKey, cancellationToken);

        // Update variant
        variant.ImageKey = null;
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetImage(
        string objectKey,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        // Get presigned URL and redirect browser to MinIO
        var presignedUrl = await storage.GetPresignedUrlAsync("warp-catalog", objectKey, 3600, cancellationToken);
        return Results.Redirect(presignedUrl);
    }

    // ── Video Endpoints ───────────────────────────────────────────────────────

    private static async Task<IResult> UploadProductVideo(
        Guid productId,
        IFormFile video,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        // Validate the product belongs to the tenant
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (product is null)
            return Results.NotFound(new { message = "Product not found." });

        // Validate content type
        if (!AllowedVideoContentTypes.Contains(video.ContentType.ToLowerInvariant()))
            return Results.BadRequest(new { message = $"Invalid content type. Allowed: {string.Join(", ", AllowedVideoContentTypes)}" });

        // Validate size
        if (video.Length > MaxVideoSize)
            return Results.BadRequest(new { message = $"Video size exceeds maximum allowed ({MaxVideoSize / 1024 / 1024}MB)." });

        // Generate object key with tenant prefix and videos subdirectory
        var extension = Path.GetExtension(video.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            extension = video.ContentType switch
            {
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "video/quicktime" => ".mov",
                "video/x-msvideo" => ".avi",
                _ => ".mp4"
            };
        }

        var objectKey = $"{tenantId}/products/{productId}/videos/{Guid.NewGuid()}{extension}";

        // Ensure bucket exists
        await storage.EnsureBucketExistsAsync("warp-catalog", cancellationToken);

        // Delete old video if exists
        if (!string.IsNullOrEmpty(product.VideoKey))
        {
            await storage.DeleteAsync("warp-catalog", product.VideoKey, cancellationToken);
        }

        // Upload new video
        using var stream = video.OpenReadStream();
        await storage.UploadAsync("warp-catalog", objectKey, stream, video.ContentType, video.Length, cancellationToken);

        // Update product
        product.VideoKey = objectKey;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { videoKey = objectKey });
    }

    private static async Task<IResult> DeleteProductVideo(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (product is null)
            return Results.NotFound(new { message = "Product not found." });

        if (string.IsNullOrEmpty(product.VideoKey))
            return Results.NotFound(new { message = "Product has no video." });

        // Delete from storage
        await storage.DeleteAsync("warp-catalog", product.VideoKey, cancellationToken);

        // Update product
        product.VideoKey = null;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UploadVariantVideo(
        Guid variantId,
        IFormFile video,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        // Validate the variant belongs to the tenant
        var variant = await db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.TenantId == tenantId.Value, cancellationToken);
        if (variant is null)
            return Results.NotFound(new { message = "Product variant not found." });

        // Validate content type
        if (!AllowedVideoContentTypes.Contains(video.ContentType.ToLowerInvariant()))
            return Results.BadRequest(new { message = $"Invalid content type. Allowed: {string.Join(", ", AllowedVideoContentTypes)}" });

        // Validate size
        if (video.Length > MaxVideoSize)
            return Results.BadRequest(new { message = $"Video size exceeds maximum allowed ({MaxVideoSize / 1024 / 1024}MB)." });

        // Generate object key with tenant prefix and videos subdirectory
        var extension = Path.GetExtension(video.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            extension = video.ContentType switch
            {
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "video/quicktime" => ".mov",
                "video/x-msvideo" => ".avi",
                _ => ".mp4"
            };
        }

        var objectKey = $"{tenantId}/variants/{variantId}/videos/{Guid.NewGuid()}{extension}";

        // Ensure bucket exists
        await storage.EnsureBucketExistsAsync("warp-catalog", cancellationToken);

        // Delete old video if exists
        if (!string.IsNullOrEmpty(variant.VideoKey))
        {
            await storage.DeleteAsync("warp-catalog", variant.VideoKey, cancellationToken);
        }

        // Upload new video
        using var stream = video.OpenReadStream();
        await storage.UploadAsync("warp-catalog", objectKey, stream, video.ContentType, video.Length, cancellationToken);

        // Update variant
        variant.VideoKey = objectKey;
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { videoKey = objectKey });
    }

    private static async Task<IResult> DeleteVariantVideo(
        Guid variantId,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var variant = await db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.TenantId == tenantId.Value, cancellationToken);
        if (variant is null)
            return Results.NotFound(new { message = "Product variant not found." });

        if (string.IsNullOrEmpty(variant.VideoKey))
            return Results.NotFound(new { message = "Product variant has no video." });

        // Delete from storage
        await storage.DeleteAsync("warp-catalog", variant.VideoKey, cancellationToken);

        // Update variant
        variant.VideoKey = null;
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetVideo(
        string objectKey,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        // Get presigned URL with longer expiry for video streaming (24 hours)
        var presignedUrl = await storage.GetPresignedUrlAsync("warp-catalog", objectKey, 86400, cancellationToken);
        return Results.Redirect(presignedUrl);
    }
}
