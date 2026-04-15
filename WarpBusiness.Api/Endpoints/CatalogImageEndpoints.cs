using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;
using WarpBusiness.Storage;

namespace WarpBusiness.Api.Endpoints;

public record ProductMediaResponse(
    Guid Id,
    string ObjectKey,
    MediaType MediaType,
    string FileName,
    string ContentType,
    int SortOrder,
    DateTimeOffset CreatedAt);

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
        var media = app.MapGroup("/api/catalog");

        media.MapPost("/products/{productId:guid}/media", UploadProductMedia)
            .WithName("UploadProductMedia")
            .RequireAuthorization()
            .DisableAntiforgery();

        media.MapGet("/products/{productId:guid}/media", GetProductMedia)
            .WithName("GetProductMedia")
            .AllowAnonymous();

        media.MapPost("/variants/{variantId:guid}/media", UploadVariantMedia)
            .WithName("UploadVariantMedia")
            .RequireAuthorization()
            .DisableAntiforgery();

        media.MapGet("/variants/{variantId:guid}/media", GetVariantMedia)
            .WithName("GetVariantMedia")
            .AllowAnonymous();

        media.MapDelete("/media/{mediaId:guid}", DeleteMedia)
            .WithName("DeleteCatalogMedia")
            .RequireAuthorization();

        media.MapGet("/media/{mediaId:guid}", GetMediaRedirect)
            .WithName("GetCatalogMediaRedirect")
            .AllowAnonymous();
    }

    private static async Task<IResult> UploadProductMedia(
        Guid productId,
        IFormFile file,
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

        var (mediaType, extension, validationError) = ValidateAndResolveFile(file);
        if (validationError is not null)
            return Results.BadRequest(new { message = validationError });

        var objectKey = mediaType == MediaType.Video
            ? $"{tenantId}/products/{productId}/videos/{Guid.NewGuid()}{extension}"
            : $"{tenantId}/products/{productId}/{Guid.NewGuid()}{extension}";

        await storage.EnsureBucketExistsAsync("warp-catalog", cancellationToken);
        using var stream = file.OpenReadStream();
        await storage.UploadAsync("warp-catalog", objectKey, stream, file.ContentType, file.Length, cancellationToken);

        var sortOrder = await db.ProductMedia.CountAsync(m => m.ProductId == productId, cancellationToken);

        var productMedia = new ProductMedia
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProductId = productId,
            ObjectKey = objectKey,
            MediaType = mediaType,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.ProductMedia.Add(productMedia);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(productMedia));
    }

    private static async Task<IResult> GetProductMedia(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var mediaList = await db.ProductMedia
            .Where(m => m.ProductId == productId && m.TenantId == tenantId.Value)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.CreatedAt)
            .Select(m => ToResponse(m))
            .ToListAsync(cancellationToken);

        return Results.Ok(mediaList);
    }

    private static async Task<IResult> UploadVariantMedia(
        Guid variantId,
        IFormFile file,
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

        var (mediaType, extension, validationError) = ValidateAndResolveFile(file);
        if (validationError is not null)
            return Results.BadRequest(new { message = validationError });

        var objectKey = mediaType == MediaType.Video
            ? $"{tenantId}/variants/{variantId}/videos/{Guid.NewGuid()}{extension}"
            : $"{tenantId}/variants/{variantId}/{Guid.NewGuid()}{extension}";

        await storage.EnsureBucketExistsAsync("warp-catalog", cancellationToken);
        using var stream = file.OpenReadStream();
        await storage.UploadAsync("warp-catalog", objectKey, stream, file.ContentType, file.Length, cancellationToken);

        var sortOrder = await db.ProductMedia.CountAsync(m => m.VariantId == variantId, cancellationToken);

        var variantMedia = new ProductMedia
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            VariantId = variantId,
            ObjectKey = objectKey,
            MediaType = mediaType,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.ProductMedia.Add(variantMedia);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(variantMedia));
    }

    private static async Task<IResult> GetVariantMedia(
        Guid variantId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var mediaList = await db.ProductMedia
            .Where(m => m.VariantId == variantId && m.TenantId == tenantId.Value)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.CreatedAt)
            .Select(m => ToResponse(m))
            .ToListAsync(cancellationToken);

        return Results.Ok(mediaList);
    }

    private static async Task<IResult> DeleteMedia(
        Guid mediaId,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var item = await db.ProductMedia
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.TenantId == tenantId.Value, cancellationToken);
        if (item is null)
            return Results.NotFound(new { message = "Media not found." });

        await storage.DeleteAsync("warp-catalog", item.ObjectKey, cancellationToken);
        db.ProductMedia.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetMediaRedirect(
        Guid mediaId,
        HttpContext httpContext,
        CatalogDbContext db,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var item = await db.ProductMedia
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.TenantId == tenantId.Value, cancellationToken);
        if (item is null)
            return Results.NotFound(new { message = "Media not found." });

        var expiry = item.MediaType == MediaType.Video ? 86400 : 3600;
        var presignedUrl = await storage.GetPresignedUrlAsync("warp-catalog", item.ObjectKey, expiry, cancellationToken);
        return Results.Redirect(presignedUrl);
    }

    private static (MediaType MediaType, string Extension, string? Error) ValidateAndResolveFile(IFormFile file)
    {
        var contentType = file.ContentType.ToLowerInvariant();

        if (contentType.StartsWith("image/"))
        {
            if (!AllowedImageContentTypes.Contains(contentType))
                return (MediaType.Image, "", $"Invalid image type. Allowed: {string.Join(", ", AllowedImageContentTypes)}");
            if (file.Length > MaxImageSize)
                return (MediaType.Image, "", $"Image size exceeds maximum allowed ({MaxImageSize / 1024 / 1024}MB).");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                ext = contentType switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/gif" => ".gif", "image/webp" => ".webp", _ => ".jpg" };
            return (MediaType.Image, ext, null);
        }
        else if (contentType.StartsWith("video/"))
        {
            if (!AllowedVideoContentTypes.Contains(contentType))
                return (MediaType.Video, "", $"Invalid video type. Allowed: {string.Join(", ", AllowedVideoContentTypes)}");
            if (file.Length > MaxVideoSize)
                return (MediaType.Video, "", $"Video size exceeds maximum allowed ({MaxVideoSize / 1024 / 1024}MB).");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                ext = contentType switch { "video/mp4" => ".mp4", "video/webm" => ".webm", "video/quicktime" => ".mov", "video/x-msvideo" => ".avi", _ => ".mp4" };
            return (MediaType.Video, ext, null);
        }

        return (MediaType.Image, "", "File must be an image or video.");
    }

    private static ProductMediaResponse ToResponse(ProductMedia m) =>
        new(m.Id, m.ObjectKey, m.MediaType, m.FileName, m.ContentType, m.SortOrder, m.CreatedAt);
}
