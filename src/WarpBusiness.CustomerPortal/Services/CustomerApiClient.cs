using System.Net.Http.Json;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.CustomerPortal.Services;

public class CustomerApiClient(HttpClient httpClient)
{
    private string? _accessToken;

    public void SetAccessToken(string token)
    {
        _accessToken = token;
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAccessToken()
    {
        _accessToken = null;
        httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/auth/login", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AuthResponse>();
        }
        catch { return null; }
    }

    public async Task<AuthProviderInfo?> GetAuthProviderAsync()
    {
        try { return await httpClient.GetFromJsonAsync<AuthProviderInfo>("api/auth/provider"); }
        catch { return null; }
    }

    // Customers can only fetch their own contact (the API will enforce this in future)
    public async Task<ContactDto?> GetMyContactAsync()
    {
        try { return await httpClient.GetFromJsonAsync<ContactDto>("api/contacts/me"); }
        catch { return null; }
    }

    public async Task RefreshTokenAsync()
    {
        try { await httpClient.PostAsync("api/auth/refresh", null); }
        catch { /* silent */ }
    }
}
