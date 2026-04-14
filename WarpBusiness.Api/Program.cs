using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Endpoints;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Services;
using WarpBusiness.Crm.Data;
using WarpBusiness.Employees.Data;
using WarpBusiness.Employees.Endpoints;
using WarpBusiness.Employees.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// PostgreSQL + Entity Framework Core
builder.AddNpgsqlDbContext<WarpBusinessDbContext>("warpdb");
builder.AddNpgsqlDbContext<EmployeeDbContext>("warpdb");
builder.AddNpgsqlDbContext<CrmDbContext>("warpdb");

builder.Services.AddScoped<IUserValidator, UserValidator>();

// Keycloak JWT Bearer authentication
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer("keycloak", realm: "warpbusiness", options =>
    {
        options.Audience = "warpbusiness-api";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters.RoleClaimType = "roles";
    });

// Diagnostic logging for JWT authentication failures
builder.Services.AddOptions<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .Configure<ILoggerFactory>((options, loggerFactory) =>
    {
        var logger = loggerFactory.CreateLogger("JwtBearerAuth");
        options.Events ??= new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents();

        options.Events.OnAuthenticationFailed = context =>
        {
            logger.LogError(context.Exception,
                "[JWT] Authentication FAILED for {Path}: {Error}",
                context.Request.Path, context.Exception.Message);
            return Task.CompletedTask;
        };

        options.Events.OnTokenValidated = context =>
        {
            var claims = context.Principal?.Claims
                .Select(c => $"{c.Type}={c.Value[..Math.Min(30, c.Value.Length)]}")
                .Take(10);
            logger.LogInformation("[JWT] Token VALIDATED for {Path}. Claims: {Claims}",
                context.Request.Path, string.Join(", ", claims ?? []));
            return Task.CompletedTask;
        };

        options.Events.OnChallenge = context =>
        {
            logger.LogWarning("[JWT] Challenge issued for {Path}. Error: {Error}, Description: {Desc}. " +
                "Auth header present: {HasAuth}",
                context.Request.Path,
                context.Error ?? "none",
                context.ErrorDescription ?? "none",
                context.Request.Headers.Authorization.Count > 0);
            return Task.CompletedTask;
        };

        options.Events.OnMessageReceived = context =>
        {
            var token = context.Token ?? context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                var display = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? token[7..Math.Min(27, token.Length)] + "..."
                    : token[..Math.Min(20, token.Length)] + "...";
                logger.LogDebug("[JWT] Token received for {Path}: {Prefix}",
                    context.Request.Path, display);
            }
            else
            {
                logger.LogWarning("[JWT] No token received for {Path}", context.Request.Path);
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SystemAdministrator", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            // Check flat "roles" claim from Keycloak (configured via protocol mapper)
            if (context.User.IsInRole("SystemAdministrator"))
                return true;
            if (context.User.HasClaim("roles", "SystemAdministrator"))
                return true;

            // Fallback: check DB-backed role (added by role enrichment middleware)
            return context.User.HasClaim("app_role", "SystemAdministrator");
        });
    });

// Keycloak Admin API service
builder.Services.AddHttpClient<KeycloakAdminService>(client =>
{
    var keycloakUrl = builder.Configuration["services:keycloak:http:0"]
        ?? builder.Configuration["services:keycloak:https:0"]
        ?? "http://localhost:8080";
    client.BaseAddress = new Uri(keycloakUrl);
});

// Database initialization (migrations + seed)
builder.Services.AddHostedService<DbInitializer>();
builder.Services.AddHostedService<EmployeeDbInitializer>();
builder.Services.AddHostedService<CrmDbInitializer>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.UseHttpsRedirection();

app.UseAuthentication();

// Role enrichment middleware: adds app_role claim from DB for authorization policies.
// MUST run BEFORE UseAuthorization() so claims are available during policy evaluation.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        var email = context.User.FindFirst("email")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        if (!string.IsNullOrEmpty(sub) || !string.IsNullOrEmpty(email))
        {
            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WarpBusinessDbContext>();

            ApplicationUser? user = null;
            if (!string.IsNullOrEmpty(sub))
                user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakSubjectId == sub);
            if (user is null && !string.IsNullOrEmpty(email))
                user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user is not null)
            {
                var identity = context.User.Identity as System.Security.Claims.ClaimsIdentity;
                identity?.AddClaim(new System.Security.Claims.Claim("app_role", user.Role.ToString()));
            }
        }
    }

    await next();
});

app.UseAuthorization();

// Tenant context middleware: validates X-Tenant-Id header and sets TenantId in HttpContext.Items
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // Paths that don't require a tenant context
    var tenantExemptPaths = new[]
    {
        "/api/users/me",
        "/api/tenants",
        "/api/currencies",
        "/health",
        "/alive"
    };

    var requiresTenant = !tenantExemptPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
        && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

    if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader)
        && Guid.TryParse(tenantIdHeader.FirstOrDefault(), out var tenantId)
        && context.User.Identity?.IsAuthenticated == true)
    {
        using var scope = context.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WarpBusinessDbContext>();

        // SystemAdministrators can access any tenant
        var isAdmin = context.User.HasClaim("app_role", "SystemAdministrator")
            || context.User.HasClaim("roles", "SystemAdministrator")
            || context.User.IsInRole("SystemAdministrator");

        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive);

        if (tenantExists)
        {
            if (isAdmin)
            {
                context.Items["TenantId"] = tenantId;
            }
            else
            {
                var sub = context.User.FindFirst("sub")?.Value;
                var email = context.User.FindFirst("email")?.Value
                    ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                ApplicationUser? user = null;
                if (!string.IsNullOrEmpty(sub))
                    user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakSubjectId == sub);
                if (user is null && !string.IsNullOrEmpty(email))
                    user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user is not null)
                {
                    var isMember = await db.UserTenantMemberships
                        .AnyAsync(m => m.UserId == user.Id && m.TenantId == tenantId);

                    if (isMember)
                    {
                        context.Items["TenantId"] = tenantId;
                    }
                    else if (requiresTenant)
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsJsonAsync(new { message = "You are not a member of this tenant." });
                        return;
                    }
                }
            }
        }
        else if (requiresTenant)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid or inactive tenant." });
            return;
        }
    }

    await next();
});

// User management API endpoints
app.MapUserEndpoints();

// Tenant management API endpoints
app.MapTenantEndpoints();

// Employee management API endpoints
app.MapEmployeeEndpoints();

// Employee-User linking API endpoints
app.MapEmployeeUserEndpoints();

// Currency management API endpoints
app.MapCurrencyEndpoints();

// Customer management API endpoints
app.MapCustomerEndpoints();

// Portal customer API endpoints (customer-scoped)
app.MapPortalCustomerEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.RequireAuthorization();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
