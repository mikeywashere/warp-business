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

// ── Product Type DTOs ─────────────────────────────────────────────────────────

public record CatalogProductTypeResponse(
    Guid Id, Guid TenantId, string Name, string? Description, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    List<CatalogProductTypeAttributeResponse> Attributes);

public record CatalogProductTypeAttributeResponse(
    Guid AttributeTypeId, string AttributeTypeName, string ValueType,
    string? Unit, bool HasColorPicker, bool IsRequired, int SortOrder);

public record CreateCatalogProductTypeRequest(string Name, string? Description = null);
public record UpdateCatalogProductTypeRequest(string Name, string? Description = null, bool? IsActive = null);
public record AssignCatalogProductTypeAttributeRequest(Guid AttributeTypeId, bool IsRequired = false, int SortOrder = 0);

// ── Attribute Type DTOs ───────────────────────────────────────────────────────

public record CatalogAttributeTypeResponse(
    Guid Id, Guid TenantId, string Name, string ValueType, string? Unit, bool HasColorPicker,
    int SortOrder, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    List<CatalogAttributeOptionResponse> Options);

public record CatalogAttributeOptionResponse(
    Guid Id, Guid AttributeTypeId, Guid TenantId, string Value, string? HexCode,
    int SortOrder, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateCatalogAttributeTypeRequest(
    string Name,
    string ValueType,
    string? Unit = null,
    bool HasColorPicker = false,
    int SortOrder = 0);

public record UpdateCatalogAttributeTypeRequest(
    string Name,
    string? Unit = null,
    bool? HasColorPicker = null,
    int? SortOrder = null,
    bool? IsActive = null);

public record CreateCatalogAttributeOptionRequest(string Value, string? HexCode = null, int SortOrder = 0);
public record UpdateCatalogAttributeOptionRequest(string Value, string? HexCode = null, int? SortOrder = null, bool? IsActive = null);

// ── Warning DTOs ──────────────────────────────────────────────────────────────

public record CatalogWarningResponse(
    Guid Id, Guid TenantId, string Name, string? Description, string? Icon, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateCatalogWarningRequest(string Name, string? Description = null, string? Icon = null);
public record UpdateCatalogWarningRequest(string Name, string? Description = null, string? Icon = null, bool? IsActive = null);

// ── Product DTOs ──────────────────────────────────────────────────────────────

public record CatalogProductWarningResponse(Guid WarningId, string Name, string? Description, string? Icon);

public record CatalogProductResponse(
    Guid Id, Guid TenantId, Guid? CategoryId, string? CategoryName,
    string Name, string? Description, string? Brand, string? SKU,
    decimal BasePrice, string Currency, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int VariantCount, Guid? ThumbnailMediaId = null,
    Guid? ProductTypeId = null, string? ProductTypeName = null,
    List<CatalogProductWarningResponse>? Warnings = null);

public record CreateCatalogProductRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    string? Description = null,
    string? Brand = null,
    string? SKU = null,
    Guid? CategoryId = null,
    Guid? ProductTypeId = null,
    List<Guid>? WarningIds = null);

public record UpdateCatalogProductRequest(
    string Name,
    decimal BasePrice,
    string Currency,
    string? Description = null,
    string? Brand = null,
    string? SKU = null,
    Guid? CategoryId = null,
    bool? IsActive = null,
    Guid? ProductTypeId = null,
    List<Guid>? WarningIds = null);

// ── Variant DTOs ──────────────────────────────────────────────────────────────

public record CatalogVariantAttributeValueResponse(
    Guid AttributeTypeId,
    string AttributeTypeName,
    string ValueType,
    string? Unit,
    bool HasColorPicker,
    Guid? AttributeOptionId,
    string? OptionValue,
    string? OptionHexCode,
    string? TextValue,
    decimal? NumberValue);

public record CatalogVariantAttributeValueRequest(
    Guid AttributeTypeId,
    Guid? AttributeOptionId = null,
    string? TextValue = null,
    decimal? NumberValue = null);

public record CatalogProductVariantResponse(
    Guid Id, Guid ProductId, Guid TenantId,
    string? SKU, decimal? Price, int StockQuantity, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    Guid? ThumbnailMediaId = null,
    List<CatalogVariantAttributeValueResponse>? Attributes = null);

public record CreateCatalogVariantRequest(
    string? SKU = null,
    decimal? Price = null,
    int StockQuantity = 0,
    List<CatalogVariantAttributeValueRequest>? Attributes = null);

public record UpdateCatalogVariantRequest(
    string? SKU = null,
    decimal? Price = null,
    int? StockQuantity = null,
    bool? IsActive = null,
    List<CatalogVariantAttributeValueRequest>? Attributes = null);

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

    // Product Types
    public async Task<List<CatalogProductTypeResponse>> GetProductTypesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/catalog/product-types");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogProductTypeResponse>>() ?? [];
    }

    public async Task<CatalogProductTypeResponse> CreateProductTypeAsync(CreateCatalogProductTypeRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/catalog/product-types", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateProductType");
        return (await response.Content.ReadFromJsonAsync<CatalogProductTypeResponse>())!;
    }

    public async Task UpdateProductTypeAsync(Guid id, UpdateCatalogProductTypeRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/product-types/{id}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateProductType");
    }

    public async Task<string?> DeleteProductTypeAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/product-types/{id}");
        var response = await _httpClient.SendAsync(msg);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return body?.Message;
        }
        await ThrowOnErrorAsync(response, "DeleteProductType");
        return null;
    }

    public async Task AssignProductTypeAttributeAsync(Guid productTypeId, AssignCatalogProductTypeAttributeRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/product-types/{productTypeId}/attributes", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "AssignProductTypeAttribute");
    }

    public async Task RemoveProductTypeAttributeAsync(Guid productTypeId, Guid attributeTypeId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/product-types/{productTypeId}/attributes/{attributeTypeId}");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "RemoveProductTypeAttribute");
    }

    // Attribute Types
    public async Task<List<CatalogAttributeTypeResponse>> GetAttributeTypesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/catalog/attribute-types");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogAttributeTypeResponse>>() ?? [];
    }

    public async Task<CatalogAttributeTypeResponse> CreateAttributeTypeAsync(CreateCatalogAttributeTypeRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/catalog/attribute-types", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateAttributeType");
        return (await response.Content.ReadFromJsonAsync<CatalogAttributeTypeResponse>())!;
    }

    public async Task UpdateAttributeTypeAsync(Guid id, UpdateCatalogAttributeTypeRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/attribute-types/{id}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateAttributeType");
    }

    public async Task<string?> DeleteAttributeTypeAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/attribute-types/{id}");
        var response = await _httpClient.SendAsync(msg);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return body?.Message;
        }
        await ThrowOnErrorAsync(response, "DeleteAttributeType");
        return null;
    }

    public async Task<List<CatalogAttributeOptionResponse>> GetAttributeOptionsAsync(Guid attributeTypeId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/catalog/attribute-types/{attributeTypeId}/options");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogAttributeOptionResponse>>() ?? [];
    }

    public async Task<CatalogAttributeOptionResponse> CreateAttributeOptionAsync(Guid attributeTypeId, CreateCatalogAttributeOptionRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/attribute-types/{attributeTypeId}/options", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateAttributeOption");
        return (await response.Content.ReadFromJsonAsync<CatalogAttributeOptionResponse>())!;
    }

    public async Task UpdateAttributeOptionAsync(Guid attributeTypeId, Guid optionId, UpdateCatalogAttributeOptionRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/attribute-types/{attributeTypeId}/options/{optionId}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateAttributeOption");
    }

    public async Task<string?> DeleteAttributeOptionAsync(Guid attributeTypeId, Guid optionId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/attribute-types/{attributeTypeId}/options/{optionId}");
        var response = await _httpClient.SendAsync(msg);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return body?.Message;
        }
        await ThrowOnErrorAsync(response, "DeleteAttributeOption");
        return null;
    }

    // Warnings
    public async Task<List<CatalogWarningResponse>> GetWarningsAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/catalog/warnings");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CatalogWarningResponse>>() ?? [];
    }

    public async Task<CatalogWarningResponse> CreateWarningAsync(CreateCatalogWarningRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/catalog/warnings", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "CreateWarning");
        return (await response.Content.ReadFromJsonAsync<CatalogWarningResponse>())!;
    }

    public async Task UpdateWarningAsync(Guid id, UpdateCatalogWarningRequest req)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/catalog/warnings/{id}", JsonContent.Create(req));
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "UpdateWarning");
    }

    public async Task<string?> DeleteWarningAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/warnings/{id}");
        var response = await _httpClient.SendAsync(msg);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return body?.Message;
        }
        await ThrowOnErrorAsync(response, "DeleteWarning");
        return null;
    }

    public async Task AddProductWarningAsync(Guid productId, Guid warningId)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/catalog/products/{productId}/warnings/{warningId}");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "AddProductWarning");
    }

    public async Task RemoveProductWarningAsync(Guid productId, Guid warningId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/catalog/products/{productId}/warnings/{warningId}");
        var response = await _httpClient.SendAsync(msg);
        await ThrowOnErrorAsync(response, "RemoveProductWarning");
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
