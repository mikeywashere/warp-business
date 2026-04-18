using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace WarpBusiness.EmployeePortal.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TokenRefreshService _tokenRefresh;
    private readonly ILogger<AuthTokenHandler> _logger;

    public AuthTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        TokenRefreshService tokenRefresh,
        ILogger<AuthTokenHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenRefresh = tokenRefresh;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogWarning("[AuthTokenHandler] No HttpContext, proceeding without token");
            return await base.SendAsync(request, cancellationToken);
        }

        var token = await httpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogDebug("[AuthTokenHandler] Attached access_token to request");
        }
        else
        {
            _logger.LogWarning("[AuthTokenHandler] No access_token found in authentication properties");
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogInformation("[AuthTokenHandler] 401 Unauthorized, attempting token refresh");

            var refreshed = await _tokenRefresh.RefreshTokenAsync();
            if (refreshed)
            {
                var newToken = await httpContext.GetTokenAsync("access_token");
                if (!string.IsNullOrEmpty(newToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                    _logger.LogInformation("[AuthTokenHandler] Retrying request with refreshed token");
                    response = await base.SendAsync(request, cancellationToken);
                }
            }
            else
            {
                _logger.LogWarning("[AuthTokenHandler] Token refresh failed, user must re-authenticate");
            }
        }

        return response;
    }
}
