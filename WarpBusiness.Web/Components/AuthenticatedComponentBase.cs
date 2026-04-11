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

    protected sealed override async Task OnInitializedAsync()
    {
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
        // Interactive phase: restore token persisted during SSR prerender
        if (PersistentState.TryTakeFromJson<PersistedTokenData>("__auth_tokens", out var data)
            && data is not null)
        {
            if (!string.IsNullOrEmpty(data.AccessToken))
                TokenProvider.AccessToken = data.AccessToken;
            if (!string.IsNullOrEmpty(data.SelectedTenantId))
                TokenProvider.SelectedTenantId = data.SelectedTenantId;
        }

        // SSR phase: HttpContext is available — capture the token
        var httpContext = HttpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            if (string.IsNullOrEmpty(TokenProvider.AccessToken))
            {
                var token = await httpContext.GetTokenAsync("access_token");
                if (!string.IsNullOrEmpty(token))
                    TokenProvider.AccessToken = token;
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
            PersistentState.PersistAsJson("__auth_tokens", new PersistedTokenData(
                TokenProvider.AccessToken, TokenProvider.SelectedTenantId));
            return Task.CompletedTask;
        });
    }

    private record PersistedTokenData(string? AccessToken, string? SelectedTenantId);
}
