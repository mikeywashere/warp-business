using System.Net.Http.Headers;
using System.Net.Http.Json;
using WarpBusiness.Api.Models;

namespace WarpBusiness.CustomerPortal.Services;

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

public class CustomerPortalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<CustomerPortalApiClient> _logger;

    public CustomerPortalApiClient(
        HttpClient httpClient,
        TokenProvider tokenProvider,
        ILogger<CustomerPortalApiClient> logger)
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
            _logger.LogDebug("[CustomerPortalApiClient] Token applied to {Method} {Uri}",
                method, uri);
        }
        else
        {
            _logger.LogWarning("[CustomerPortalApiClient] No token in TokenProvider for {Method} {Uri}",
                method, uri);
        }
        return request;
    }

    public async Task<CustomerDto?> GetProfileAsync(Guid customerId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/portal/customers/{customerId}");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CustomerDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[CustomerPortalApiClient] GetProfile failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<CustomerDto> UpdateProfileAsync(Guid customerId, CustomerUpdateDto dto)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Put, $"api/portal/customers/{customerId}",
                JsonContent.Create(dto));
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[CustomerPortalApiClient] UpdateProfile failed: {StatusCode}",
                    (int)response.StatusCode);
                throw new ApiException((int)response.StatusCode, body);
            }

            return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[CustomerPortalApiClient] UpdateProfile HTTP error");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<CustomerDto> UploadLogoAsync(Guid customerId, byte[] logoBytes, string mimeType)
    {
        try
        {
            var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(logoBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            form.Add(fileContent, "logoFile", "logo");

            using var request = CreateRequest(HttpMethod.Post, $"api/portal/customers/{customerId}/logo", form);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[CustomerPortalApiClient] UploadLogo failed: {StatusCode}",
                    (int)response.StatusCode);
                throw new ApiException((int)response.StatusCode, body);
            }

            return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[CustomerPortalApiClient] UploadLogo HTTP error");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<byte[]?> GetLogoAsync(Guid customerId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/portal/customers/{customerId}/logo");
            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[CustomerPortalApiClient] GetLogo failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task DeleteLogoAsync(Guid customerId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, $"api/portal/customers/{customerId}/logo");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[CustomerPortalApiClient] DeleteLogo failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }

    public async Task<List<CustomerEmployeeDto>> GetAssignmentsAsync(Guid customerId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/portal/customers/{customerId}/assignments");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<CustomerEmployeeDto>>() ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[CustomerPortalApiClient] GetAssignments failed");
            throw new ApiException((int?)ex.StatusCode ?? 500, ex.Message);
        }
    }
}
