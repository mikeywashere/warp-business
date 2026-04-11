# Skill: Blazor Server API Client with Token Forwarding

## When to Use
When a Blazor Server app needs to call an authenticated API using the current user's OIDC access token.

## Pattern

### 1. AuthTokenHandler (DelegatingHandler)
Reads the access_token from `HttpContext` via `IHttpContextAccessor` and attaches it as a Bearer header. Also forwards tenant context from a cookie.

```csharp
public class AuthTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public AuthTokenHandler(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var token = await httpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Forward tenant context from cookie
            if (httpContext.Request.Cookies.TryGetValue("X-Selected-Tenant", out var tenantId)
                && !string.IsNullOrEmpty(tenantId))
                request.Headers.Add("X-Tenant-Id", tenantId);
        }
        return await base.SendAsync(request, ct);
    }
}
```

### 2. Registration in Program.cs
```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthTokenHandler>();
builder.Services.AddHttpClient<MyApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["services:api:https:0"] ?? "http://localhost:5000");
})
.AddHttpMessageHandler<AuthTokenHandler>();
```

### 3. Cookie-Based Context for DelegatingHandlers
When a DelegatingHandler (transient) needs scoped state from Blazor circuits:
- Set state via a minimal API endpoint that writes an HttpOnly cookie
- Read cookie in the handler via `IHttpContextAccessor`
- Track display-friendly state in a scoped service (`TenantStateService`) for Blazor components

```csharp
// Minimal API to set cookie
app.MapPost("/select-tenant", (HttpContext ctx, SelectTenantRequest req) =>
{
    ctx.Response.Cookies.Append("X-Selected-Tenant", req.TenantId.ToString(), new CookieOptions
    {
        HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/",
        Expires = DateTimeOffset.UtcNow.AddHours(12)
    });
    return Results.Ok();
}).RequireAuthorization();
```

## Gotchas
- Requires `SaveTokens = true` in OIDC options (already set in this project)
- `IHttpContextAccessor` is not available inside Blazor SignalR hub context after initial render — works for SSR and initial load
- When nesting `EditForm` inside `AuthorizeView`, rename the context parameter: `<Authorized Context="authContext">`
- DelegatingHandlers are transient — they can't inject scoped services directly. Use `HttpContext` (cookies, items) as the bridge between scoped Blazor state and transient HTTP pipeline components.
