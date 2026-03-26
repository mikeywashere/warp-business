using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.CustomerPortal.Services;

public class CustomerApiClient(HttpClient httpClient, CustomerAuthState authState, NavigationManager navigationManager)
{
    private readonly HttpClient _http = httpClient;
    private readonly CustomerAuthState _authState = authState;
    private readonly NavigationManager _nav = navigationManager;

    // Auth

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AuthResponse>();
        }
        catch { return null; }
    }

    public async Task<AuthProviderInfo?> GetAuthProviderAsync()
    {
        try { return await _http.GetFromJsonAsync<AuthProviderInfo>("api/auth/provider"); }
        catch { return null; }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _http.PostAsync("api/auth/logout", null);
        }
        catch { /* silent — clear state regardless */ }
        finally
        {
            _http.DefaultRequestHeaders.Authorization = null;
            _authState.ClearAuth();
            _nav.NavigateTo("/");
        }
    }

    private async Task<bool> TryRefreshAsync()
    {
        try
        {
            // Cookie is HttpOnly — browser sends it automatically
            var response = await _http.PostAsync("api/auth/refresh", null);
            if (!response.IsSuccessStatusCode) return false;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth == null) return false;

            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
            _authState.SetAuth(auth);
            return true;
        }
        catch { return false; }
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(Func<Task<HttpResponseMessage>> request)
    {
        var response = await request();

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Try to refresh — if successful, retry once
            if (await TryRefreshAsync())
            {
                response = await request();
            }
            else
            {
                // Refresh failed — session is dead
                _authState.ClearAuth();
                _nav.NavigateTo("/login");
            }
        }

        return response;
    }

    // Contacts

    public async Task<ContactDto?> GetMyContactAsync()
    {
        try
        {
            var response = await SendWithRefreshAsync(() => _http.GetAsync("api/contacts/me"));
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<ContactDto>()
                : null;
        }
        catch { return null; }
    }

    public async Task<ContactDto?> UpdateMyContactAsync(Guid id, UpdateContactRequest request)
    {
        try
        {
            var response = await SendWithRefreshAsync(() => _http.PutAsJsonAsync($"api/contacts/{id}", request));
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<ContactDto>()
                : null;
        }
        catch { return null; }
    }
}
