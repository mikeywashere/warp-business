using WarpBusiness.Shared.Auth;

namespace WarpBusiness.Web.Services;

public class AuthStateService
{
    private AuthResponse? _currentAuth;

    public bool IsAuthenticated => _currentAuth is not null;
    public string? UserEmail => _currentAuth?.Email;
    public string? UserFullName => _currentAuth?.FullName;
    public IList<string> Roles => _currentAuth?.Roles ?? [];
    public string? Token => _currentAuth?.Token;

    public event Action? OnChange;

    public void SetAuth(AuthResponse auth)
    {
        _currentAuth = auth;
        OnChange?.Invoke();
    }

    public void ClearAuth()
    {
        _currentAuth = null;
        OnChange?.Invoke();
    }
}
