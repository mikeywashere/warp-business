using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace WarpBusiness.Web.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthTokenHandler> _logger;

    public AuthTokenHandler(IHttpContextAccessor httpContextAccessor, ILogger<AuthTokenHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var requestUri = request.RequestUri?.PathAndQuery ?? "unknown";

        if (httpContext != null)
        {
            // SSR phase: HttpContext is available — get token from auth cookie
            var token = await httpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogDebug("[AuthTokenHandler] SSR token applied for {Uri} (token starts: {TokenPrefix}...)",
                    requestUri, token[..Math.Min(20, token.Length)]);
            }
            else
            {
                _logger.LogWarning("[AuthTokenHandler] SSR phase but GetTokenAsync returned null for {Uri}", requestUri);
            }

            // Forward the selected tenant ID from cookie to the API
            if (httpContext.Request.Cookies.TryGetValue("X-Selected-Tenant", out var tenantId)
                && !string.IsNullOrEmpty(tenantId))
            {
                if (!request.Headers.Contains("X-Tenant-Id"))
                    request.Headers.Add("X-Tenant-Id", tenantId);
            }
        }
        else
        {
            // Circuit phase: no HttpContext. Typed client's CreateRequest() should have set headers.
            if (request.Headers.Authorization is not null)
            {
                var scheme = request.Headers.Authorization.Scheme;
                var param = request.Headers.Authorization.Parameter;
                _logger.LogDebug("[AuthTokenHandler] Circuit phase — typed client set auth header for {Uri} ({Scheme}, token starts: {TokenPrefix}...)",
                    requestUri, scheme, param?[..Math.Min(20, param.Length)] ?? "null");
            }
            else
            {
                _logger.LogWarning("[AuthTokenHandler] Circuit phase — NO auth header for {Uri}. Token was not set by typed client.", requestUri);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogError("[AuthTokenHandler] 401 Unauthorized from API for {Method} {Uri}. " +
                "Auth header present: {HasAuth}, WWW-Authenticate: {WwwAuth}",
                request.Method, requestUri,
                request.Headers.Authorization is not null,
                response.Headers.WwwAuthenticate.ToString());
        }

        return response;
    }
}
