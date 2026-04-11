# SKILL: Blazor Server Automatic Token Refresh with Keycloak

## Problem

In a Blazor Server (SSR + interactive circuit) application using OIDC + cookie auth, the access token is stored in the cookie at login and never refreshed. After expiry, all API calls fail with `401 invalid_token`. The Keycloak session is still valid; only the access token needs to be renewed.

## Architecture

The token lifecycle in Blazor Server has two distinct phases:

| Phase | HttpContext | Token source | Can write cookie |
|-------|-------------|--------------|-----------------|
| SSR prerender | ✅ available | `httpContext.GetTokenAsync("access_token")` | ✅ Yes |
| SignalR circuit | ❌ null | `TokenProvider.AccessToken` | ❌ No |

## Two-Layer Refresh Strategy

### Layer 1 — Proactive (SSR phase, `AuthenticatedComponentBase`)

Before transferring the token to the circuit, check if it is near-expiry:

```csharp
private static bool IsTokenNearExpiry(string token, int bufferSeconds = 60)
{
    var parts = token.Split('.');
    if (parts.Length != 3) return false;
    var payload = parts[1].Replace('-', '+').Replace('_', '/');
    payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
    var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
    using var doc = JsonDocument.Parse(json);
    if (doc.RootElement.TryGetProperty("exp", out var expEl))
        return DateTimeOffset.FromUnixTimeSeconds(expEl.GetInt64()) <= DateTimeOffset.UtcNow.AddSeconds(bufferSeconds);
    return false;
}
```

If near-expiry, call `TokenRefreshService`, update the auth cookie, then proceed with the fresh token. No extra NuGet packages needed — just base64url decode the JWT payload.

### Layer 2 — Reactive (`AuthTokenHandler`)

After any `401` response with `WWW-Authenticate: Bearer error="invalid_token"`:

1. Get the refresh token:
   - SSR: `await httpContext.GetTokenAsync("refresh_token")`
   - Circuit: `_tokenProvider.RefreshToken`
2. Call `TokenRefreshService.RefreshAsync(refreshToken)`
3. Update `TokenProvider.AccessToken` + `TokenProvider.RefreshToken`
4. SSR only: update the auth cookie via `SignInAsync`
5. Clone the original request with the new token and retry **once**

**Always `LoadIntoBufferAsync` before the first send** so request body can be re-read on retry.

## TokenRefreshService

```csharp
// POST {keycloakUrl}/realms/{realm}/protocol/openid-connect/token
// form: grant_type=refresh_token&client_id={clientId}&refresh_token={refreshToken}
// public client — NO client_secret needed
```

Use a **dedicated named `HttpClient`** (`"keycloak-token"`). Never reuse the API typed client pipeline — it has `AuthTokenHandler` in it which would create a circular call.

## Auth Cookie Update Pattern (SSR)

```csharp
var authResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
if (authResult.Succeeded && authResult.Principal is not null)
{
    var props = authResult.Properties!;
    props.UpdateTokenValue("access_token", newAccessToken);
    props.UpdateTokenValue("refresh_token", newRefreshToken);
    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authResult.Principal, props);
}
```

## DI Registration

```csharp
// Named HttpClient for Keycloak token endpoint (no auth handler in pipeline)
builder.Services.AddHttpClient("keycloak-token")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = isDevelopment
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null
    });

// Transient refresh service (stateless, safe)
builder.Services.AddTransient<TokenRefreshService>();
```

## Token Persistence (SSR → Circuit)

Persist **both** tokens via `PersistentComponentState`:

```csharp
// Persist during prerender
PersistentState.PersistAsJson("__auth_tokens", new PersistedTokenData(
    TokenProvider.AccessToken,
    TokenProvider.RefreshToken,
    TokenProvider.SelectedTenantId));

// Restore in interactive phase
if (PersistentState.TryTakeFromJson<PersistedTokenData>("__auth_tokens", out var data))
{
    TokenProvider.AccessToken = data.AccessToken;
    TokenProvider.RefreshToken = data.RefreshToken;
}
```

Also capture refresh token in `CircuitHandler.OnCircuitOpenedAsync`:

```csharp
var refreshToken = await httpContext.GetTokenAsync("refresh_token");
if (!string.IsNullOrEmpty(refreshToken))
    _tokenProvider.RefreshToken = refreshToken;
```

## Key Files (this project)

- `WarpBusiness.Web/Services/TokenRefreshService.cs`
- `WarpBusiness.Web/Services/AuthTokenHandler.cs`
- `WarpBusiness.Web/Services/TokenProvider.cs`
- `WarpBusiness.Web/Services/TokenCircuitHandler.cs`
- `WarpBusiness.Web/Components/AuthenticatedComponentBase.cs`
- `WarpBusiness.Web/Program.cs`

## Constraints / Notes

- Circuit phase cannot write HTTP cookies (no `HttpContext`). Token is updated in `TokenProvider` memory only; next page load re-reads from Keycloak's refreshed session.
- Retry happens **once** per request — no retry loop risk.
- `JsonContent.Create()` is replayable (re-serializes on each send), but `LoadIntoBufferAsync` ensures safety for any content type.
- JWT expiry check requires no additional packages — parse the payload directly.
