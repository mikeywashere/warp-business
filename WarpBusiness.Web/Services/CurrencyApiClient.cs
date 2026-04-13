using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WarpBusiness.Web.Services;

// DTOs
public record CurrencyResponse(string Code, string Name, string? Symbol, string? NumericCode, int? MinorUnit, bool IsActive);
public record UpdateCurrencyRequest(bool IsActive);
public record RefreshCurrenciesResponse(string Message);

public class CurrencyApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<CurrencyApiClient> _logger;

    public CurrencyApiClient(HttpClient httpClient, TokenProvider tokenProvider, ILogger<CurrencyApiClient> logger)
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
            _logger.LogWarning("[CurrencyApiClient] No token in TokenProvider for {Method} {Uri}", method, uri);
        }
        if (!string.IsNullOrEmpty(_tokenProvider.SelectedTenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tokenProvider.SelectedTenantId);
        return request;
    }

    public async Task<List<CurrencyResponse>> GetAllCurrenciesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/currencies");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CurrencyResponse>>() ?? [];
    }

    public async Task<List<CurrencyResponse>> GetActiveCurrenciesAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/currencies/active");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CurrencyResponse>>() ?? [];
    }

    public async Task UpdateCurrencyAsync(string code, UpdateCurrencyRequest updateRequest)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/currencies/{code}", JsonContent.Create(updateRequest));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> RefreshCurrenciesAsync()
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/currencies/refresh");
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RefreshCurrenciesResponse>();
        return result?.Message ?? "Refresh complete.";
    }
}
