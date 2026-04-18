using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WarpBusiness.Api.Endpoints;

namespace WarpBusiness.EmployeePortal.Services;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(int statusCode, string? body) : base(ParseMessage(statusCode, body)) { StatusCode = statusCode; }
    private static string ParseMessage(int code, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString() ?? body;
            }
            catch { if (body.Length <= 300) return body; }
        }
        return code switch { 403 => "Access denied.", 404 => "Not found.", 500 => "Server error.", _ => $"Request failed ({code})." };
    }
}

public class EmployeePortalApiClient
{
    private readonly HttpClient _http;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<EmployeePortalApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public EmployeePortalApiClient(HttpClient http, TokenProvider tokenProvider, ILogger<EmployeePortalApiClient> logger)
    {
        _http = http;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, uri) { Content = content };
        if (!string.IsNullOrEmpty(_tokenProvider.AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.AccessToken);
        return req;
    }

    public async Task<EmployeePortalProfileResponse?> GetMeAsync()
    {
        using var req = CreateRequest(HttpMethod.Get, "api/employee-portal/me");
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<EmployeePortalProfileResponse>(JsonOptions);
    }

    public async Task<List<PortalShiftResponse>> GetScheduleAsync(DateOnly? from = null, DateOnly? to = null)
    {
        var url = "api/employee-portal/me/schedule";
        if (from.HasValue && to.HasValue)
            url += $"?from={from.Value:yyyy-MM-dd}&to={to.Value:yyyy-MM-dd}";
        using var req = CreateRequest(HttpMethod.Get, url);
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<List<PortalShiftResponse>>(JsonOptions) ?? [];
    }

    public async Task<List<PortalHoursResponse>> GetHoursAsync()
    {
        using var req = CreateRequest(HttpMethod.Get, "api/employee-portal/me/hours");
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<List<PortalHoursResponse>>(JsonOptions) ?? [];
    }

    public async Task<List<PortalAvailabilityResponse>> GetAvailabilityAsync()
    {
        using var req = CreateRequest(HttpMethod.Get, "api/employee-portal/me/availability");
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<List<PortalAvailabilityResponse>>(JsonOptions) ?? [];
    }

    public async Task<PortalAvailabilityResponse?> UpsertAvailabilityAsync(int dayOfWeek, PortalAvailabilityRequest request)
    {
        using var req = CreateRequest(HttpMethod.Put, $"api/employee-portal/me/availability/{dayOfWeek}", JsonContent.Create(request));
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<PortalAvailabilityResponse>(JsonOptions);
    }

    public async Task DeleteAvailabilityAsync(int dayOfWeek)
    {
        using var req = CreateRequest(HttpMethod.Delete, $"api/employee-portal/me/availability/{dayOfWeek}");
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    public async Task<List<PortalTimeOffResponse>> GetTimeOffAsync()
    {
        using var req = CreateRequest(HttpMethod.Get, "api/employee-portal/me/time-off");
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<List<PortalTimeOffResponse>>(JsonOptions) ?? [];
    }

    public async Task<PortalTimeOffResponse?> RequestTimeOffAsync(PortalTimeOffRequest request)
    {
        using var req = CreateRequest(HttpMethod.Post, "api/employee-portal/me/time-off", JsonContent.Create(request));
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<PortalTimeOffResponse>(JsonOptions);
    }

    public async Task CancelTimeOffAsync(Guid id)
    {
        using var req = CreateRequest(HttpMethod.Delete, $"api/employee-portal/me/time-off/{id}");
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new ApiException((int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }
}
