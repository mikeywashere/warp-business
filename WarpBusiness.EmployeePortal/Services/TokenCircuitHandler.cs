using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace WarpBusiness.EmployeePortal.Services;

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

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogWarning("[TokenCircuitHandler] No HttpContext on circuit connection");
            return;
        }

        _tokenProvider.AccessToken = await httpContext.GetTokenAsync("access_token");
        _tokenProvider.RefreshToken = await httpContext.GetTokenAsync("refresh_token");

        _logger.LogDebug("[TokenCircuitHandler] Cached tokens for circuit");
    }
}
