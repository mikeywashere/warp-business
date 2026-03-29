using System.Net.Http.Json;
using WarpBusiness.MobileApp.Helpers;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.MobileApp.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private string? _currentToken;
    private string? _userEmail;
    private string? _fullName;
    private IList<string>? _roles;
    private Guid? _selectedTenantId;

    public string? CurrentToken => _currentToken;
    public string? UserEmail => _userEmail;
    public string? FullName => _fullName;
    public IList<string>? Roles => _roles;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_currentToken);
    public bool IsAdmin => _roles?.Contains("Admin") == true;
    public Guid? SelectedTenantId => _selectedTenantId;

    public event Action? AuthStateChanged;

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _currentToken = await SecureStorage.GetAsync(Constants.AuthTokenKey);
            _userEmail = await SecureStorage.GetAsync(Constants.UserEmailKey);
            _fullName = await SecureStorage.GetAsync(Constants.UserNameKey);
            var rolesStr = await SecureStorage.GetAsync(Constants.UserRolesKey);
            if (!string.IsNullOrEmpty(rolesStr))
                _roles = rolesStr.Split(',').ToList();
            var tenantStr = await SecureStorage.GetAsync(Constants.SelectedTenantIdKey);
            if (Guid.TryParse(tenantStr, out var tid))
                _selectedTenantId = tid;
        }
        catch { /* SecureStorage may fail on some platforms */ }
        AuthStateChanged?.Invoke();
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth != null) await SaveAuthStateAsync(auth);
        return auth;
    }

    public async Task<AuthResponse?> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", new RegisterRequest(email, password, firstName, lastName));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth != null) await SaveAuthStateAsync(auth);
        return auth;
    }

    public async Task<AuthResponse?> GetMeAsync()
    {
        var response = await _httpClient.GetAsync("api/auth/me");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<AuthResponse?> RefreshTokenAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/auth/refresh", null);
            if (!response.IsSuccessStatusCode) return null;
            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth != null) await SaveAuthStateAsync(auth);
            return auth;
        }
        catch { return null; }
    }

    public async Task LogoutAsync()
    {
        try { await _httpClient.PostAsync("api/auth/logout", null); } catch { }
        _currentToken = null;
        _userEmail = null;
        _fullName = null;
        _roles = null;
        _selectedTenantId = null;
        SecureStorage.RemoveAll();
        AuthStateChanged?.Invoke();
    }

    public async Task<AuthResponse?> SelectTenantAsync(Guid tenantId)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/select-tenant", new SelectTenantRequest(tenantId));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth != null)
        {
            _selectedTenantId = tenantId;
            await SecureStorage.SetAsync(Constants.SelectedTenantIdKey, tenantId.ToString());
            await SaveAuthStateAsync(auth);
        }
        return auth;
    }

    public async Task<IEnumerable<MyTenantDto>?> GetMyTenantsAsync()
    {
        var response = await _httpClient.GetAsync("api/auth/my-tenants");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<MyTenantDto>>();
    }

    public async Task<AuthProviderInfo?> GetProviderAsync()
    {
        var response = await _httpClient.GetAsync("api/auth/provider");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthProviderInfo>();
    }

    private async Task SaveAuthStateAsync(AuthResponse auth)
    {
        _currentToken = auth.Token;
        _userEmail = auth.Email;
        _fullName = auth.FullName;
        _roles = auth.Roles;
        try
        {
            if (auth.Token != null) await SecureStorage.SetAsync(Constants.AuthTokenKey, auth.Token);
            if (auth.Email != null) await SecureStorage.SetAsync(Constants.UserEmailKey, auth.Email);
            if (auth.FullName != null) await SecureStorage.SetAsync(Constants.UserNameKey, auth.FullName);
            if (auth.Roles != null) await SecureStorage.SetAsync(Constants.UserRolesKey, string.Join(",", auth.Roles));
        }
        catch { }
        AuthStateChanged?.Invoke();
    }
}
