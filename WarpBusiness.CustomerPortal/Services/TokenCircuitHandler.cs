using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace WarpBusiness.CustomerPortal.Services;

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
            _logger.LogWarning("[TokenCircuitHandler] Circuit opened but HttpContext is null");
            return;
        }

        _logger.LogInformation("[TokenCircuitHandler] Circuit opened — HttpContext available");

        var token = await httpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
        {
            _tokenProvider.AccessToken = token;
            _logger.LogInformation("[TokenCircuitHandler] Token captured");
        }
        else
        {
            _logger.LogWarning("[TokenCircuitHandler] GetTokenAsync returned null");
        }

        var refreshToken = await httpContext.GetTokenAsync("refresh_token");
        if (!string.IsNullOrEmpty(refreshToken))
            _tokenProvider.RefreshToken = refreshToken;
    }
}
