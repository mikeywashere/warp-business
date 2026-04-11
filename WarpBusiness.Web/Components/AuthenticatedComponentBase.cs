using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using System.Text.Json;
using WarpBusiness.Web.Services;

namespace WarpBusiness.Web.Components;

/// <summary>
/// Base class for Blazor pages that need authenticated API access.
/// Handles transferring the access and refresh tokens from the SSR prerender phase
/// to the interactive SignalR circuit via PersistentComponentState.
/// Also performs a proactive token refresh when the access token is near-expiry.
/// </summary>
public abstract class AuthenticatedComponentBase : ComponentBase
{
    [Inject] private PersistentComponentState PersistentState { get; set; } = default!;
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    [Inject] protected TokenProvider TokenProvider { get; set; } = default!;
    [Inject] private TokenRefreshService TokenRefreshService { get; set; } = default!;
    [Inject] private ILoggerFactory LoggerFactory { get; set; } = default!;

    private ILogger? _logger;

    protected sealed override async Task OnInitializedAsync()
    {
        _logger = LoggerFactory.CreateLogger<AuthenticatedComponentBase>();
        await RestoreOrCaptureTokenAsync();
        await OnAuthenticatedInitializedAsync();
    }

    /// <summary>
    /// Override this instead of OnInitializedAsync. Called after the auth
    /// token has been restored from persistence or captured from HttpContext.
    /// </summary>
    protected virtual Task OnAuthenticatedInitializedAsync() => Task.CompletedTask;

    private async Task RestoreOrCaptureTokenAsync()
    {
        var hasHttpContext = HttpContextAccessor.HttpContext is not null;
        _logger?.LogInformation("[AuthBase] RestoreOrCaptureToken — HttpContext available: {HasCtx}, " +
            "TokenProvider already has token: {HasToken}",
            hasHttpContext, !string.IsNullOrEmpty(TokenProvider.AccessToken));

        // Interactive phase: restore tokens persisted during SSR prerender
        if (PersistentState.TryTakeFromJson<PersistedTokenData>("__auth_tokens", out var data)
            && data is not null)
        {
            _logger?.LogInformation("[AuthBase] PersistentComponentState restored — " +
                "has access token: {HasToken}, has refresh token: {HasRefresh}, has tenant: {HasTenant}",
                !string.IsNullOrEmpty(data.AccessToken),
                !string.IsNullOrEmpty(data.RefreshToken),
                !string.IsNullOrEmpty(data.SelectedTenantId));

            if (!string.IsNullOrEmpty(data.AccessToken))
            {
                TokenProvider.AccessToken = data.AccessToken;
                _logger?.LogDebug("[AuthBase] Access token restored from persistence (starts: {Prefix}...)",
                    data.AccessToken[..Math.Min(20, data.AccessToken.Length)]);
            }
            if (!string.IsNullOrEmpty(data.RefreshToken))
                TokenProvider.RefreshToken = data.RefreshToken;
            if (!string.IsNullOrEmpty(data.SelectedTenantId))
                TokenProvider.SelectedTenantId = data.SelectedTenantId;
        }
        else
        {
            _logger?.LogInformation("[AuthBase] PersistentComponentState had no persisted token data");
        }

        // SSR phase: HttpContext is available — capture and potentially refresh tokens
        var httpContext = HttpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            if (string.IsNullOrEmpty(TokenProvider.AccessToken))
            {
                var token = await httpContext.GetTokenAsync("access_token");
                var refreshToken = await httpContext.GetTokenAsync("refresh_token");

                if (!string.IsNullOrEmpty(token))
                {
                    // Proactive refresh: don't hand a near-expired token to the circuit
                    if (IsTokenNearExpiry(token) && !string.IsNullOrEmpty(refreshToken))
                    {
                        _logger?.LogInformation("[AuthBase] SSR: access token is near-expiry — proactively refreshing before circuit transfer");
                        var refreshResult = await TokenRefreshService.RefreshAsync(refreshToken);
                        if (refreshResult is not null)
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
                                    _logger?.LogInformation("[AuthBase] SSR: auth cookie updated after proactive refresh");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "[AuthBase] SSR: failed to update auth cookie after proactive refresh");
                            }

                            token = refreshResult.AccessToken;
                            refreshToken = refreshResult.RefreshToken;
                        }
                        else
                        {
                            _logger?.LogWarning("[AuthBase] SSR: proactive refresh failed — proceeding with existing (near-expired) token");
                        }
                    }

                    TokenProvider.AccessToken = token;
                    TokenProvider.RefreshToken = refreshToken;
                    _logger?.LogInformation("[AuthBase] SSR: captured tokens from HttpContext (access starts: {Prefix}...)",
                        token[..Math.Min(20, token.Length)]);
                }
                else
                {
                    _logger?.LogWarning("[AuthBase] SSR: GetTokenAsync returned null — user may not be authenticated");
                }
            }
            else
            {
                _logger?.LogDebug("[AuthBase] SSR: TokenProvider already has token, skipping HttpContext capture");
            }

            if (string.IsNullOrEmpty(TokenProvider.SelectedTenantId)
                && httpContext.Request.Cookies.TryGetValue("X-Selected-Tenant", out var tenantId)
                && !string.IsNullOrEmpty(tenantId))
            {
                TokenProvider.SelectedTenantId = tenantId;
            }
        }

        // Register persistence callback (only fires during prerender)
        PersistentState.RegisterOnPersisting(() =>
        {
            _logger?.LogInformation("[AuthBase] Persisting tokens for circuit transfer — " +
                "has access token: {HasToken}, has refresh token: {HasRefresh}",
                !string.IsNullOrEmpty(TokenProvider.AccessToken),
                !string.IsNullOrEmpty(TokenProvider.RefreshToken));
            PersistentState.PersistAsJson("__auth_tokens", new PersistedTokenData(
                TokenProvider.AccessToken,
                TokenProvider.RefreshToken,
                TokenProvider.SelectedTenantId));
            return Task.CompletedTask;
        });

        _logger?.LogInformation("[AuthBase] Final state — TokenProvider has token: {HasToken}, has refresh: {HasRefresh}, tenant: {HasTenant}",
            !string.IsNullOrEmpty(TokenProvider.AccessToken),
            !string.IsNullOrEmpty(TokenProvider.RefreshToken),
            !string.IsNullOrEmpty(TokenProvider.SelectedTenantId));
    }

    /// <summary>
    /// Returns true if the JWT access token is expired or will expire within <paramref name="bufferSeconds"/>.
    /// Parses the <c>exp</c> claim from the token payload without full JWT validation.
    /// </summary>
    private static bool IsTokenNearExpiry(string token, int bufferSeconds = 60)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            var payload = parts[1];
            // Base64url → standard Base64
            var padded = payload
                .Replace('-', '+')
                .Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                var exp = expElement.GetInt64();
                var expiry = DateTimeOffset.FromUnixTimeSeconds(exp);
                return expiry <= DateTimeOffset.UtcNow.AddSeconds(bufferSeconds);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private record PersistedTokenData(string? AccessToken, string? RefreshToken, string? SelectedTenantId);
}
