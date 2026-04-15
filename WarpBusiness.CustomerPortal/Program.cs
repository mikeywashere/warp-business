using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using WarpBusiness.CustomerPortal.Components;
using WarpBusiness.CustomerPortal.Services;

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
    options.ClientId = "warpbusiness-customer-portal";
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.MapInboundClaims = false;
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

    options.TokenValidationParameters.NameClaimType = "preferred_username";
    options.TokenValidationParameters.RoleClaimType = "roles";

    options.Events = new OpenIdConnectEvents
    {
        OnTokenResponseReceived = context =>
        {
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

            context.ProtocolMessage.ClientId = "warpbusiness-customer-portal";

            var request = context.HttpContext.Request;
            context.ProtocolMessage.PostLogoutRedirectUri = $"{request.Scheme}://{request.Host}/";
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Token cache and refresh services
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped<CircuitHandler, TokenCircuitHandler>();
builder.Services.AddTransient<TokenRefreshService>();
builder.Services.AddHttpClient("keycloak-token")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = builder.Environment.IsDevelopment()
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null
    });

// Portal services
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthTokenHandler>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LogoService>();

Action<HttpClient> configureApiClient = client =>
{
    var apiUrl = builder.Configuration["services:api:https:0"]
        ?? builder.Configuration["services:api:http:0"]
        ?? "http://localhost:5000";
    client.BaseAddress = new Uri(apiUrl);
};

builder.Services.AddHttpClient<CustomerPortalApiClient>(configureApiClient)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

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
        RedirectUri = returnUrl ?? "/"
    }, [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapGet("/logout", async (HttpContext context) =>
{
    var idToken = await context.GetTokenAsync("id_token");

    if (string.IsNullOrEmpty(idToken))
        idToken = context.Request.Cookies["X-IdToken-Cache"];

    var absoluteRedirect = $"{context.Request.Scheme}://{context.Request.Host}/";

    var signOutProperties = new AuthenticationProperties
    {
        RedirectUri = absoluteRedirect
    };

    if (!string.IsNullOrEmpty(idToken))
    {
        signOutProperties.Items[".Token.id_token"] = idToken;
    }

    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, signOutProperties);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Cookies.Delete("X-IdToken-Cache");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
