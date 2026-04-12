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
.AddCookie()
.AddOpenIdConnect(options =>
{
    var keycloakUrl = builder.Configuration["services:keycloak:https:0"]
        ?? builder.Configuration["services:keycloak:http:0"]
        ?? "http://localhost:8080";

    Console.WriteLine($"[Web Startup] Keycloak URL resolved to: {keycloakUrl}");
    Console.WriteLine($"[Web Startup] OIDC Authority: {keycloakUrl}/realms/warpbusiness");

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
    Console.WriteLine($"[Web Startup] API base URL resolved to: {apiUrl}");
    client.BaseAddress = new Uri(apiUrl);
};

builder.Services.AddHttpClient<UserApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddHttpClient<TenantApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

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
    context.Response.Cookies.Delete("X-Selected-Tenant");
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/"
    });
});

// Tenant selection endpoints
app.MapPost("/select-tenant", (HttpContext context, SelectTenantRequest request) =>
{
    context.Response.Cookies.Append("X-Selected-Tenant", request.TenantId.ToString(), new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.AddHours(12)
    });
    return Results.Ok();
}).RequireAuthorization();

app.MapPost("/clear-tenant", (HttpContext context) =>
{
    context.Response.Cookies.Delete("X-Selected-Tenant");
    return Results.Ok();
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
