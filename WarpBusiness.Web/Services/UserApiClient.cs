using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WarpBusiness.Web.Services;

public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string RoleName,
    DateTime CreatedAt);

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string RoleName);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string RoleName);

public record UpdateProfileRequest(
    string FirstName,
    string LastName);

public class UserApiClient
{
    private readonly HttpClient _httpClient;

    public UserApiClient(HttpClient httpClient, TokenProvider tokenProvider)
    {
        _httpClient = httpClient;

        // In Blazor Server interactive mode, HttpContext is unavailable so the
        // AuthTokenHandler can't set auth headers. Apply cached token/tenant as
        // default headers so every request carries them automatically.
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

    public async Task<List<UserResponse>> GetUsersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<UserResponse>>("api/users")
            ?? [];
    }

    public async Task<UserResponse?> GetUserAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<UserResponse>($"api/users/{id}");
    }

    public async Task<UserResponse?> GetCurrentUserAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserResponse>("api/users/me");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    public async Task UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/users/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("api/users/me", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/users/{id}");
        response.EnsureSuccessStatusCode();
    }
}
