using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace WarpBusiness.Web.Services;

/// <summary>
/// Captures the access token and tenant cookie from the initial HTTP request
/// when the Blazor Server circuit opens. At circuit-open time, IHttpContextAccessor
/// still has the HttpContext from the WebSocket upgrade request (which carries cookies).
/// </summary>
public class TokenCircuitHandler : CircuitHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TokenProvider _tokenProvider;

    public TokenCircuitHandler(IHttpContextAccessor httpContextAccessor, TokenProvider tokenProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenProvider = tokenProvider;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        var token = await httpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
        {
            _tokenProvider.AccessToken = token;
        }

        if (httpContext.Request.Cookies.TryGetValue("X-Selected-Tenant", out var tenantId)
            && !string.IsNullOrEmpty(tenantId))
        {
            _tokenProvider.SelectedTenantId = tenantId;
        }
    }
}
