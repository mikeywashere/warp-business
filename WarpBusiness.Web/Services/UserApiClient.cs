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
    private readonly TokenProvider _tokenProvider;

    public UserApiClient(HttpClient httpClient, TokenProvider tokenProvider)
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

    public async Task<List<UserResponse>> GetUsersAsync()
    {
        using var request = CreateRequest(HttpMethod.Get, "api/users");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UserResponse>>() ?? [];
    }

    public async Task<UserResponse?> GetUserAsync(Guid id)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/users/{id}");
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task<UserResponse?> GetCurrentUserAsync()
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "api/users/me");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserResponse>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Post, "api/users", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    public async Task UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Put, $"api/users/{id}", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateProfileAsync(UpdateProfileRequest request)
    {
        using var msg = CreateRequest(HttpMethod.Put, "api/users/me", JsonContent.Create(request));
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        using var msg = CreateRequest(HttpMethod.Delete, $"api/users/{id}");
        var response = await _httpClient.SendAsync(msg);
        response.EnsureSuccessStatusCode();
    }
}
