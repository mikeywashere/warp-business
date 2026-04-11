namespace WarpBusiness.Web.Services;

/// <summary>
/// Caches the access and refresh tokens from the initial HTTP request so they remain
/// available after the Blazor Server circuit switches to SignalR
/// (where HttpContext is no longer accessible).
/// </summary>
public class TokenProvider
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? SelectedTenantId { get; set; }
}
