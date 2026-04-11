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

    public TenantApiClient(HttpClient httpClient, TokenProvider tokenProvider)
    {
        _httpClient = httpClient;

        if (!string.IsNullOrEmpty(tokenProvider.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenProvider.AccessToken);
        }

        if (!string.IsNullOrEmpty(tokenProvider.SelectedTenantId))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", tokenProvider.SelectedTenantId);
        }
    }

    public async Task<List<UserTenantResponse>> GetMyTenantsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<UserTenantResponse>>("api/users/me/tenants")
            ?? [];
    }

    public async Task<List<TenantResponse>> GetTenantsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<TenantResponse>>("api/tenants")
            ?? [];
    }

    public async Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tenants", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>())!;
    }

    public async Task UpdateTenantAsync(Guid id, UpdateTenantRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tenants/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTenantAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/tenants/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<TenantMemberResponse>> GetTenantMembersAsync(Guid tenantId)
    {
        return await _httpClient.GetFromJsonAsync<List<TenantMemberResponse>>($"api/tenants/{tenantId}/members")
            ?? [];
    }

    public async Task AddTenantMemberAsync(Guid tenantId, AddTenantMemberRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tenants/{tenantId}/members", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveTenantMemberAsync(Guid tenantId, Guid userId)
    {
        var response = await _httpClient.DeleteAsync($"api/tenants/{tenantId}/members/{userId}");
        response.EnsureSuccessStatusCode();
    }
}
