using WarpBusiness.Shared.Auth;

namespace WarpBusiness.CustomerPortal.Services;

public class CustomerAuthState
{
    private AuthResponse? _auth;

    public bool IsAuthenticated => _auth != null;
    public string? UserFullName => _auth?.FullName;
    public string? Email => _auth?.Email;
    public string? Token => _auth?.Token;

    public event Action? OnChange;

    public void SetAuth(AuthResponse auth)
    {
        _auth = auth;
        OnChange?.Invoke();
    }

    public void ClearAuth()
    {
        _auth = null;
        OnChange?.Invoke();
    }
}
