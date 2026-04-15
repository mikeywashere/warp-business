using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WarpBusiness.Web.Services;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CatalogCategoryResponse(
    Guid Id, Guid TenantId, Guid? ParentCategoryId,
    string Name, string? Description, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int SubCategoryCount, int ProductCount);

public record CreateCatalogCategoryRequest(
    string Name,
    string? Description = null,
    Guid? ParentCategoryId = null);

public record UpdateCatalogCategoryRequest(
    string Name,
    string? Description = null,
    Guid? ParentCategoryId = null,
    bool? IsActive = null);

public record CatalogColorResponse(
    Guid Id, Guid TenantId,
    string Name, string? HexCode, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateCatalogColorRequest(string Name, string? HexCode = null);

public record UpdateCatalogColorRequest(string Name, string? HexCode = null, bool? IsActive = null);

public record CatalogSizeResponse(
    Guid Id, Guid TenantId,
    string Name, string SizeType, int SortOrder, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateCatalogSizeRequest(string Name, string? SizeType = null, int SortOrder = 0);

public record UpdateCatalogSizeRequest(string Name, string? SizeType = null, int? SortOrder = null, bool? IsActive = null);

public record CatalogProductResponse(
    Guid Id, Guid TenantId, Guid? CategoryId, string? CategoryName,
    string Name, string? Description, string? Brand, string? SKU,
    decimal BasePrice, string Currency, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int VariantCount, string? ThumbnailKey = null);

public record CreateCatalogProductRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    string? Description = null,
    string? Brand = null,
    string? SKU = null,
    Guid? CategoryId = null);

public record UpdateCatalogProductRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    string? Description = null,
    string? Brand = null,
    string? SKU = null,
    Guid? CategoryId = null,
    bool? IsActive = null);

public record CatalogProductVariantResponse(
    Guid Id, Guid ProductId, Guid TenantId,
    Guid? ColorId, string? ColorName, string? ColorHex,
    Guid? SizeId, string? SizeName, string? SizeType,
    string? SKU, decimal? Price, int StockQuantity, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? ThumbnailKey = null);

public record CreateCatalogVariantRequest(
    Guid? ColorId = null,
    Guid? SizeId = null,
    string? SKU = null,
    decimal? Price = null,
    int StockQuantity = 0);

public record UpdateCatalogVariantRequest(
    Guid? ColorId = null,
    Guid? SizeId = null,
    string? SKU = null,
    decimal? Price = null,
    int? StockQuantity = null,
    bool? IsActive = null);

// ── Media DTOs ────────────────────────────────────────────────────────────────

public enum MediaType { Image, Video }

public record ProductMediaResponse(
    Guid Id,
    string ObjectKey,
    MediaType MediaType,
    string FileName,
    string ContentType,
    int SortOrder,
    DateTimeOffset CreatedAt);

public record MediaUploadResponse(Guid Id, string ObjectKey, string MediaType, string FileName, string ContentType, int SortOrder, DateTimeOffset CreatedAt);

// ── Client ────────────────────────────────────────────────────────────────────

public class CatalogApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<CatalogApiClient> _logger;

    public CatalogApiClient(HttpClient httpClient, TokenProvider tokenProvider, ILogger<CatalogApiClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        if (!string.IsNullOrEmpty(_tokenProvider.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.AccessToken);
        }
        else
        {
            _logger.LogWarning("[CatalogApiClient] No token in TokenProvider for {Method} {Uri}", method, uri);
        }
        if (!string.IsNullOrEmpty(_tokenProvider.SelectedTenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tokenProvider.SelectedTenantId);
        return request;
    }

    private async Task ThrowOnErrorAsync(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[CatalogApiClient] {Operation} failed with {StatusCode}: {Body}",
                operation, (int)response.StatusCode, body);
            throw new ApiException((int)response.StatusCode, body);
        }
    }

    // Categories
    public async Task<List<CatalogCategoryResponse>> GetCategoriesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/catalog/categories");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogCategoryResponse>>() ?? [];
    }

    public async Task<CatalogCategoryResponse> CreateCategoryAsync(CreateCatalogCategoryRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/catalog/categories", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateCategory");
        return (await response.Content.ReadFromJsonAsync<CatalogCategoryResponse>())!;
    }

    public async Task UpdateCategoryAsync(Guid id, UpdateCatalogCategoryRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/categories/{id}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateCategory");
    }

    public async Task<string?> DeleteCategoryAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/categories/{id}");
        var response = await _httpClient.SendAsync(msg);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return body?.Message;
        }
        await ThrowOnErrorAsync(response, "DeleteCategory");
        return null;
    }

    // Colors
    public async Task<List<CatalogColorResponse>> GetColorsAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/catalog/colors");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogColorResponse>>() ?? [];
    }

    public async Task<CatalogColorResponse> CreateColorAsync(CreateCatalogColorRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/catalog/colors", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateColor");
        return (await response.Content.ReadFromJsonAsync<CatalogColorResponse>())!;
    }

    public async Task UpdateColorAsync(Guid id, UpdateCatalogColorRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/colors/{id}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateColor");
    }

    public async Task<string?> DeleteColorAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/colors/{id}");
        var response = await _httpClient.SendAsync(msg);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return body?.Message;
        }
        await ThrowOnErrorAsync(response, "DeleteColor");
        return null;
    }

    // Sizes
    public async Task<List<CatalogSizeResponse>> GetSizesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/catalog/sizes");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogSizeResponse>>() ?? [];
    }

    public async Task<CatalogSizeResponse> CreateSizeAsync(CreateCatalogSizeRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/catalog/sizes", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateSize");
        return (await response.Content.ReadFromJsonAsync<CatalogSizeResponse>())!;
    }

    public async Task UpdateSizeAsync(Guid id, UpdateCatalogSizeRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/sizes/{id}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateSize");
    }

    public async Task<string?> DeleteSizeAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/sizes/{id}");
        var response = await _httpClient.SendAsync(msg);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return body?.Message;
        }
        await ThrowOnErrorAsync(response, "DeleteSize");
        return null;
    }

    // Products
    public async Task<List<CatalogProductResponse>> GetProductsAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/catalog/products");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogProductResponse>>() ?? [];
    }

    public async Task<CatalogProductResponse> CreateProductAsync(CreateCatalogProductRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/catalog/products", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateProduct");
        return (await response.Content.ReadFromJsonAsync<CatalogProductResponse>())!;
    }

    public async Task UpdateProductAsync(Guid id, UpdateCatalogProductRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/products/{id}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateProduct");
    }

    public async Task DeleteProductAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/products/{id}");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "DeleteProduct");
    }

    // Product Variants
    public async Task<List<CatalogProductVariantResponse>> GetVariantsAsync(Guid productId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/catalog/products/{productId}/variants");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogProductVariantResponse>>() ?? [];
    }

    public async Task<CatalogProductVariantResponse> CreateVariantAsync(Guid productId, CreateCatalogVariantRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/products/{productId}/variants", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateVariant");
        return (await response.Content.ReadFromJsonAsync<CatalogProductVariantResponse>())!;
    }

    public async Task UpdateVariantAsync(Guid productId, Guid variantId, UpdateCatalogVariantRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/products/{productId}/variants/{variantId}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateVariant");
    }

    public async Task DeleteVariantAsync(Guid productId, Guid variantId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/products/{productId}/variants/{variantId}");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "DeleteVariant");
    }

    // Image management
    public async Task<string?> UploadProductImageAsync(Guid productId, Stream imageStream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/products/{productId}/image", content);
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UploadProductImage");

        var result = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        return result?.ImageKey;
    }

    public async Task DeleteProductImageAsync(Guid productId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/products/{productId}/image");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "DeleteProductImage");
    }

    public async Task<string?> UploadVariantImageAsync(Guid variantId, Stream imageStream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/variants/{variantId}/image", content);
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UploadVariantImage");

        var result = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        return result?.ImageKey;
    }

    public async Task DeleteVariantImageAsync(Guid variantId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/variants/{variantId}/image");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "DeleteVariantImage");
    }

    public string GetImageUrl(string imageKey)
    {
        return $"{_httpClient.BaseAddress}api/catalog/images/{imageKey}";
    }

    // Video management
    // NOTE: For large video files (up to 500MB), the API's MaxResponseContentBufferSize may need
    // to be increased. This is configured on the API side, not here in the client.
    public async Task<string?> UploadProductVideoAsync(Guid productId, Stream videoStream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(videoStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/products/{productId}/video", content);
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UploadProductVideo");

        var result = await response.Content.ReadFromJsonAsync<VideoUploadResponse>();
        return result?.VideoKey;
    }

    public async Task DeleteProductVideoAsync(Guid productId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/products/{productId}/video");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "DeleteProductVideo");
    }

    public async Task<string?> UploadVariantVideoAsync(Guid variantId, Stream videoStream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(videoStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/variants/{variantId}/video", content);
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UploadVariantVideo");

        var result = await response.Content.ReadFromJsonAsync<VideoUploadResponse>();
        return result?.VideoKey;
    }

    public async Task DeleteVariantVideoAsync(Guid variantId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/variants/{variantId}/video");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "DeleteVariantVideo");
    }

    public string GetVideoUrl(string videoKey)
    {
        return $"{_httpClient.BaseAddress}api/catalog/videos/{videoKey}";
    }

    // Media management (new multi-media API)
    public async Task<List<ProductMediaResponse>> GetProductMediaAsync(Guid productId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/catalog/products/{productId}/media");
        var response = await _httpClient.SendAsync(request);
        await ThrowOnErrorAsync(response, "GetProductMedia");
        return await response.Content.ReadFromJsonAsync<List<ProductMediaResponse>>() ?? [];
    }

    public async Task<List<ProductMediaResponse>> GetVariantMediaAsync(Guid variantId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/catalog/variants/{variantId}/media");
        var response = await _httpClient.SendAsync(request);
        await ThrowOnErrorAsync(response, "GetVariantMedia");
        return await response.Content.ReadFromJsonAsync<List<ProductMediaResponse>>() ?? [];
    }

    public async Task<ProductMediaResponse> UploadProductMediaAsync(Guid productId, Stream stream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/products/{productId}/media", content);
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UploadProductMedia");
        return (await response.Content.ReadFromJsonAsync<ProductMediaResponse>())!;
    }

    public async Task<ProductMediaResponse> UploadVariantMediaAsync(Guid variantId, Stream stream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/variants/{variantId}/media", content);
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UploadVariantMedia");
        return (await response.Content.ReadFromJsonAsync<ProductMediaResponse>())!;
    }

    public async Task DeleteMediaAsync(Guid mediaId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/media/{mediaId}");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "DeleteMedia");
    }

    public string GetMediaUrl(Guid mediaId)
        => $"{_httpClient.BaseAddress}api/catalog/media/{mediaId}";
}

public record ApiMessageResponse(string Message);
public record ImageUploadResponse(string ImageKey);
public record VideoUploadResponse(string VideoKey);
