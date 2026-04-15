using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using WarpBusiness.Web.Components;
using WarpBusiness.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure OIDC authentication with Keycloak
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.MaxAge = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
})
.AddOpenIdConnect(options =>
{
    var keycloakUrl = builder.Configuration["services:keycloak:http:0"]
        ?? builder.Configuration["services:keycloak:https:0"]
        ?? "http://localhost:8080";

    options.Authority = $"{keycloakUrl}/realms/warpbusiness";
    options.ClientId = "warpbusiness-web";
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.MapInboundClaims = false;
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    // In development, Keycloak may use HTTP
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

    options.TokenValidationParameters.NameClaimType = "preferred_username";
    options.TokenValidationParameters.RoleClaimType = "roles";

    // Ensure id_token_hint is sent to Keycloak on logout
    options.Events = new OpenIdConnectEvents
    {
        OnTokenResponseReceived = context =>
        {
            // Cache the id_token in a secure HttpOnly cookie so it survives
            // the Blazor Server WebSocket mode where GetTokenAsync returns null
            var idToken = context.TokenEndpointResponse?.IdToken;
            if (!string.IsNullOrEmpty(idToken))
            {
                context.HttpContext.Response.Cookies.Append("X-IdToken-Cache", idToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    IsEssential = true
                });
            }
            return Task.CompletedTask;
        },
        OnRedirectToIdentityProviderForSignOut = async context =>
        {
            var idToken = await context.HttpContext.GetTokenAsync("id_token");

            if (string.IsNullOrEmpty(idToken))
                idToken = context.Properties?.GetTokenValue("id_token");

            if (string.IsNullOrEmpty(idToken))
                idToken = context.HttpContext.Request.Cookies["X-IdToken-Cache"];

            if (!string.IsNullOrEmpty(idToken))
            {
                context.ProtocolMessage.IdTokenHint = idToken;
            }

            // Always send client_id as a fallback. Keycloak requires EITHER
            // id_token_hint OR client_id when post_logout_redirect_uri is used.
            context.ProtocolMessage.ClientId = "warpbusiness-web";

            // Build absolute URI so Keycloak redirects back to the web app, not to itself
            var request = context.HttpContext.Request;
            context.ProtocolMessage.PostLogoutRedirectUri = $"{request.Scheme}://{request.Host}/";
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Tenant state service (scoped per circuit/session)
builder.Services.AddScoped<TenantStateService>();

// Token cache: survives the SSR → interactive circuit transition
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped<CircuitHandler, TokenCircuitHandler>();

// Token refresh service (calls Keycloak token endpoint to exchange refresh token for new access token)
builder.Services.AddTransient<TokenRefreshService>();
builder.Services.AddHttpClient("keycloak-token")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Allow HTTP in development (Keycloak may not have TLS locally)
        ServerCertificateCustomValidationCallback = builder.Environment.IsDevelopment()
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null
    });

// HTTP client for API calls with auth token forwarding
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthTokenHandler>();

Action<HttpClient> configureApiClient = client =>
{
    var apiUrl = builder.Configuration["services:api:https:0"]
        ?? builder.Configuration["services:api:http:0"]
        ?? "http://localhost:5000";
    client.BaseAddress = new Uri(apiUrl);
};

builder.Services.AddHttpClient<UserApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<TenantApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<EmployeeApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<CurrencyApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<CrmApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<CatalogApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddSingleton<CatalogTemplateRenderer>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.MapDefaultEndpoints();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Login/logout endpoints
app.MapGet("/login", (string? returnUrl) =>
    Results.Challenge(new AuthenticationProperties
    {
        RedirectUri = returnUrl ?? "/select-tenant"
    }, [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapGet("/logout", async (HttpContext context) =>
{
    // Grab the id_token NOW — while the auth cookie is still valid and readable.
    // This must happen BEFORE any SignOutAsync calls.
    var idToken = await context.GetTokenAsync("id_token");

    // Fallback: read from the cache cookie
    if (string.IsNullOrEmpty(idToken))
        idToken = context.Request.Cookies["X-IdToken-Cache"];

    context.Response.Cookies.Delete("X-Selected-Tenant");
    context.Response.Cookies.Delete("X-Selected-Tenant-Name");

    // Build absolute URI so the cookie auth redirect goes back to the web app
    var absoluteRedirect = $"{context.Request.Scheme}://{context.Request.Host}/";

    // Stash the id_token in sign-out properties so the OIDC handler
    // finds it via properties.GetTokenValue("id_token") before our event fires
    var signOutProperties = new AuthenticationProperties
    {
        RedirectUri = absoluteRedirect
    };

    if (!string.IsNullOrEmpty(idToken))
    {
        signOutProperties.Items[".Token.id_token"] = idToken;
    }

    // Sign out of OIDC — the handler will find id_token in properties
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, signOutProperties);

    // Then clear the local auth cookie
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    // Clean up the cache cookie after sign-out handlers have used it
    context.Response.Cookies.Delete("X-IdToken-Cache");
});

// Tenant selection — browser-navigated GET sets cookies then redirects
app.MapGet("/set-tenant", (HttpContext context, Guid tenantId, string? tenantName, string? returnUrl) =>
{
    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = context.Request.IsHttps,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.AddDays(30)
    };
    context.Response.Cookies.Append("X-Selected-Tenant", tenantId.ToString(), cookieOptions);
    if (!string.IsNullOrEmpty(tenantName))
        context.Response.Cookies.Append("X-Selected-Tenant-Name", tenantName, cookieOptions);

    // Store last tenant slug in a long-lived cookie that survives logout
    context.Response.Cookies.Append("X-Last-Tenant-Slug", tenantName ?? tenantId.ToString(), new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = context.Request.IsHttps,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.AddDays(365)  // Long-lived, survives logout
    });

    // Only allow local redirects to prevent open redirect attacks
    var safeUrl = !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        ? returnUrl : "/";
    return Results.LocalRedirect(safeUrl);
}).RequireAuthorization();

app.MapPost("/clear-tenant", (HttpContext context) =>
{
    context.Response.Cookies.Delete("X-Selected-Tenant");
    context.Response.Cookies.Delete("X-Selected-Tenant-Name");
    return Results.Ok();
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
