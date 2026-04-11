using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WarpBusiness.Web.Services;

// DTOs
public record UserTenantResponse(Guid TenantId, string TenantName, string TenantSlug);
public record TenantResponse(Guid Id, string Name, string Slug, bool IsActive, DateTime CreatedAt);
public record CreateTenantRequest(string Name, string Slug);
public record UpdateTenantRequest(string Name, string Slug, bool IsActive);
public record AddTenantMemberRequest(Guid UserId);
public record TenantMemberResponse(Guid UserId, string FirstName, string LastName, string Email, string Role, DateTime JoinedAt);
public record SelectTenantRequest(Guid TenantId);

public class TenantApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;

    public TenantApiClient(HttpClient httpClient, TokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        if (!string.IsNullOrEmpty(_tokenProvider.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.AccessToken);
        if (!string.IsNullOrEmpty(_tokenProvider.SelectedTenantId))
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tokenProvider.SelectedTenantId);
        return request;
    }

    public async Task<List<UserTenantResponse>> GetMyTenantsAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/users/me/tenants");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UserTenantResponse>>() ?? [];
    }

    public async Task<List<TenantResponse>> GetTenantsAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/tenants");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TenantResponse>>() ?? [];
    }

    public async Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/tenants", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>())!;
    }

    public async Task UpdateTenantAsync(Guid id, UpdateTenantRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/tenants/{id}", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTenantAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/tenants/{id}");
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<TenantMemberResponse>> GetTenantMembersAsync(Guid tenantId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/tenants/{tenantId}/members");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TenantMemberResponse>>() ?? [];
    }

    public async Task AddTenantMemberAsync(Guid tenantId, AddTenantMemberRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Post, $"api/tenants/{tenantId}/members", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveTenantMemberAsync(Guid tenantId, Guid userId)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/tenants/{tenantId}/members/{userId}");
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }
}
