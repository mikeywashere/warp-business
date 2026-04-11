using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using WarpBusiness.Web.Services;

namespace WarpBusiness.Web.Components;

/// <summary>
/// Base class for Blazor pages that need authenticated API access.
/// Handles transferring the access token from the SSR prerender phase
/// to the interactive SignalR circuit via PersistentComponentState.
/// </summary>
public abstract class AuthenticatedComponentBase : ComponentBase
{
    [Inject] private PersistentComponentState PersistentState { get; set; } = default!;
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    [Inject] protected TokenProvider TokenProvider { get; set; } = default!;
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

        // Interactive phase: restore token persisted during SSR prerender
        if (PersistentState.TryTakeFromJson<PersistedTokenData>("__auth_tokens", out var data)
            && data is not null)
        {
            _logger?.LogInformation("[AuthBase] PersistentComponentState restored — " +
                "has access token: {HasToken}, has tenant: {HasTenant}",
                !string.IsNullOrEmpty(data.AccessToken), !string.IsNullOrEmpty(data.SelectedTenantId));

            if (!string.IsNullOrEmpty(data.AccessToken))
            {
                TokenProvider.AccessToken = data.AccessToken;
                _logger?.LogDebug("[AuthBase] Token restored from persistence (starts: {Prefix}...)",
                    data.AccessToken[..Math.Min(20, data.AccessToken.Length)]);
            }
            if (!string.IsNullOrEmpty(data.SelectedTenantId))
                TokenProvider.SelectedTenantId = data.SelectedTenantId;
        }
        else
        {
            _logger?.LogInformation("[AuthBase] PersistentComponentState had no persisted token data");
        }

        // SSR phase: HttpContext is available — capture the token
        var httpContext = HttpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            if (string.IsNullOrEmpty(TokenProvider.AccessToken))
            {
                var token = await httpContext.GetTokenAsync("access_token");
                if (!string.IsNullOrEmpty(token))
                {
                    TokenProvider.AccessToken = token;
                    _logger?.LogInformation("[AuthBase] SSR: captured token from HttpContext (starts: {Prefix}...)",
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
            _logger?.LogInformation("[AuthBase] Persisting token for circuit transfer — " +
                "has token: {HasToken}", !string.IsNullOrEmpty(TokenProvider.AccessToken));
            PersistentState.PersistAsJson("__auth_tokens", new PersistedTokenData(
                TokenProvider.AccessToken, TokenProvider.SelectedTenantId));
            return Task.CompletedTask;
        });

        _logger?.LogInformation("[AuthBase] Final state — TokenProvider has token: {HasToken}, tenant: {HasTenant}",
            !string.IsNullOrEmpty(TokenProvider.AccessToken),
            !string.IsNullOrEmpty(TokenProvider.SelectedTenantId));
    }

    private record PersistedTokenData(string? AccessToken, string? SelectedTenantId);
}
