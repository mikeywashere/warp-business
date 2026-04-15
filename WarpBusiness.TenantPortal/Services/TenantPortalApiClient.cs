using System.Net.Http.Headers;
using System.Net.Http.Json;
using WarpBusiness.Api.Models;

namespace WarpBusiness.TenantPortal.Services;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public ApiException(int statusCode, string? responseBody)
        : base(ParseUserMessage(statusCode, responseBody))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    private static string ParseUserMessage(int statusCode, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? body;
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                    return detail.GetString() ?? body;
                if (doc.RootElement.TryGetProperty("title", out var title))
                    return title.GetString() ?? body;
            }
            catch (System.Text.Json.JsonException)
            {
                if (body.Length <= 300)
                    return body;
            }
        }

        return statusCode switch
        {
            400 => "The server rejected the request. Please check your input and try again.",
            409 => "This resource already exists.",
            500 => "An internal server error occurred. Please try again or contact support.",
            _ => $"Request failed with status {statusCode}."
        };
    }
}

public class TenantPortalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<TenantPortalApiClient> _logger;

    public TenantPortalApiClient(
        HttpClient httpClient,
        TokenProvider tokenProvider,
        ILogger<TenantPortalApiClient> logger)
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
            _logger.LogDebug("[TenantPortalApiClient] Token applied to {Method} {Uri}",
                method, uri);
        }
        else
        {
            _logger.LogWarning("[TenantPortalApiClient] No token in TokenProvider for {Method} {Uri}",
                method, uri);
        }
        return request;
    }

    public async Task<TenantResponse?> GetTenantAsync(Guid tenantId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/tenants/{tenantId}");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TenantResponse>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TenantPortalApiClient] GetTenant failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<TenantResponse> UpdateSubscriptionAsync(Guid tenantId, UpdateTenantRequest dto)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Put, $"api/tenants/{tenantId}",
                JsonContent.Create(dto));
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[TenantPortalApiClient] UpdateSubscription failed: {StatusCode}",
                    (int)response.StatusCode);
                throw new ApiException((int)response.StatusCode, body);
            }

            return (await response.Content.ReadFromJsonAsync<TenantResponse>())!;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TenantPortalApiClient] UpdateSubscription HTTP error");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<TenantResponse> UploadLogoAsync(Guid tenantId, string base64, string mimeType)
    {
        try
        {
            var dto = new { LogoBase64 = base64, LogoMimeType = mimeType };
            using var request = CreateRequest(HttpMethod.Post, $"api/tenants/{tenantId}/logo",
                JsonContent.Create(dto));
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[TenantPortalApiClient] UploadLogo failed: {StatusCode}",
                    (int)response.StatusCode);
                throw new ApiException((int)response.StatusCode, body);
            }

            return (await response.Content.ReadFromJsonAsync<TenantResponse>())!;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TenantPortalApiClient] UploadLogo HTTP error");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task DeleteLogoAsync(Guid tenantId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, $"api/tenants/{tenantId}/logo");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TenantPortalApiClient] DeleteLogo failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<List<TenantRequestResponse>> GetRequestsAsync(Guid tenantId, string? search, string? status, string? type)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(search))
                queryParams.Add($"search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrEmpty(status) && status != "All")
                queryParams.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrEmpty(type) && type != "All")
                queryParams.Add($"type={Uri.EscapeDataString(type)}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            using var request = CreateRequest(HttpMethod.Get, $"api/tenants/{tenantId}/requests{query}");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<TenantRequestResponse>>() ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TenantPortalApiClient] GetRequests failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<TenantRequestResponse?> GetRequestAsync(Guid tenantId, Guid requestId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/tenants/{tenantId}/requests/{requestId}");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TenantRequestResponse>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TenantPortalApiClient] GetRequest failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<TenantRequestResponse> CreateRequestAsync(Guid tenantId, CreateTenantRequestRequest dto)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, $"api/tenants/{tenantId}/requests",
                JsonContent.Create(dto));
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[TenantPortalApiClient] CreateRequest failed: {StatusCode}",
                    (int)response.StatusCode);
                throw new ApiException((int)response.StatusCode, body);
            }

            return (await response.Content.ReadFromJsonAsync<TenantRequestResponse>())!;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TenantPortalApiClient] CreateRequest HTTP error");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task CancelRequestAsync(Guid tenantId, Guid requestId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, $"api/tenants/{tenantId}/requests/{requestId}/cancel", null);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[TenantPortalApiClient] CancelRequest failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }
}
