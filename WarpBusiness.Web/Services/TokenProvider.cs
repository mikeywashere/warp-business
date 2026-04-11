namespace WarpBusiness.Web.Services;

/// <summary>
/// Caches the access token from the initial HTTP request so it remains
/// available after the Blazor Server circuit switches to SignalR
/// (where HttpContext is no longer accessible).
/// </summary>
public class TokenProvider
{
    public string? AccessToken { get; set; }
    public string? SelectedTenantId { get; set; }
}
