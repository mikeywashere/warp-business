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
    private readonly ILogger<TokenCircuitHandler> _logger;

    public TokenCircuitHandler(
        IHttpContextAccessor httpContextAccessor,
        TokenProvider tokenProvider,
        ILogger<TokenCircuitHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogWarning("[TokenCircuitHandler] Circuit opened but HttpContext is null — " +
                "cannot capture token from WebSocket upgrade request");
            return;
        }

        _logger.LogInformation("[TokenCircuitHandler] Circuit opened — HttpContext available, " +
            "user authenticated: {IsAuth}", httpContext.User.Identity?.IsAuthenticated);

        var token = await httpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
        {
            _tokenProvider.AccessToken = token;
            _logger.LogInformation("[TokenCircuitHandler] Token captured (starts: {Prefix}...)",
                token[..Math.Min(20, token.Length)]);
        }
        else
        {
            _logger.LogWarning("[TokenCircuitHandler] GetTokenAsync returned null — " +
                "tokens may not be in auth cookie");
        }

        var refreshToken = await httpContext.GetTokenAsync("refresh_token");
        if (!string.IsNullOrEmpty(refreshToken))
            _tokenProvider.RefreshToken = refreshToken;

        if (httpContext.Request.Cookies.TryGetValue("X-Selected-Tenant", out var tenantId)
            && !string.IsNullOrEmpty(tenantId))
        {
            _tokenProvider.SelectedTenantId = tenantId;
        }
    }
}
