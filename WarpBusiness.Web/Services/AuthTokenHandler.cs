using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net;
using System.Net.Http.Headers;

namespace WarpBusiness.Web.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TokenProvider _tokenProvider;
    private readonly TokenRefreshService _tokenRefreshService;
    private readonly ILogger<AuthTokenHandler> _logger;

    public AuthTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        TokenProvider tokenProvider,
        TokenRefreshService tokenRefreshService,
        ILogger<AuthTokenHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenProvider = tokenProvider;
        _tokenRefreshService = tokenRefreshService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer content upfront so it can be replayed if we need to retry after token refresh
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

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

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var wwwAuth = response.Headers.WwwAuthenticate.ToString();
            _logger.LogError("[AuthTokenHandler] 401 Unauthorized from API for {Method} {Uri}. " +
                "Auth header present: {HasAuth}, WWW-Authenticate: {WwwAuth}",
                request.Method, requestUri,
                request.Headers.Authorization is not null,
                wwwAuth);

            // Reactive refresh: if the error is an expired token, refresh and retry once
            if (wwwAuth.Contains("invalid_token", StringComparison.OrdinalIgnoreCase))
            {
                var retried = await TryRefreshAndRetryAsync(request, httpContext, requestUri, cancellationToken);
                if (retried is not null)
                    return retried;
            }
        }

        return response;
    }

    private async Task<HttpResponseMessage?> TryRefreshAndRetryAsync(
        HttpRequestMessage originalRequest,
        HttpContext? httpContext,
        string requestUri,
        CancellationToken cancellationToken)
    {
        string? refreshToken;

        if (httpContext != null)
        {
            refreshToken = await httpContext.GetTokenAsync("refresh_token");
            _logger.LogInformation("[AuthTokenHandler] SSR phase — attempting reactive token refresh for {Uri}", requestUri);
        }
        else
        {
            refreshToken = _tokenProvider.RefreshToken;
            _logger.LogInformation("[AuthTokenHandler] Circuit phase — attempting reactive token refresh for {Uri}", requestUri);
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("[AuthTokenHandler] No refresh token available — cannot refresh for {Uri}", requestUri);
            return null;
        }

        var refreshResult = await _tokenRefreshService.RefreshAsync(refreshToken, cancellationToken);
        if (refreshResult is null)
        {
            _logger.LogWarning("[AuthTokenHandler] Token refresh service returned null — giving up on {Uri}", requestUri);
            return null;
        }

        _logger.LogInformation("[AuthTokenHandler] Token refreshed — updating token store and retrying {Uri}", requestUri);

        // Update in-memory token store (both SSR and circuit phases)
        _tokenProvider.AccessToken = refreshResult.AccessToken;
        _tokenProvider.RefreshToken = refreshResult.RefreshToken;

        // SSR phase: also persist the new tokens back to the auth cookie so the browser gets them
        if (httpContext != null)
        {
            try
            {
                var authResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                if (authResult.Succeeded && authResult.Principal is not null)
                {
                    var properties = authResult.Properties!;
                    properties.UpdateTokenValue("access_token", refreshResult.AccessToken);
                    properties.UpdateTokenValue("refresh_token", refreshResult.RefreshToken);
                    await httpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        authResult.Principal,
                        properties);
                    _logger.LogInformation("[AuthTokenHandler] Auth cookie updated with refreshed tokens");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthTokenHandler] Failed to update auth cookie — in-memory token is current but cookie remains stale");
            }
        }

        // Clone and retry with the new access token
        var retryRequest = await CloneRequestAsync(originalRequest, refreshResult.AccessToken, cancellationToken);
        var retryResponse = await base.SendAsync(retryRequest, cancellationToken);

        _logger.LogInformation("[AuthTokenHandler] Retry after refresh: {StatusCode} for {Uri}",
            retryResponse.StatusCode, requestUri);

        return retryResponse;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage original,
        string newAccessToken,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        foreach (var header in original.Headers)
        {
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync(cancellationToken);
            var byteContent = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
                byteContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = byteContent;
        }

        return clone;
    }
}
