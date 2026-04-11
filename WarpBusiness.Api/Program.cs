using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Endpoints;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// PostgreSQL + Entity Framework Core
builder.AddNpgsqlDbContext<WarpBusinessDbContext>("warpdb");

// Keycloak JWT Bearer authentication
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer("keycloak", realm: "warpbusiness", options =>
    {
        options.Audience = "warpbusiness-api";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SystemAdministrator", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            // Check realm roles from Keycloak token
            var realmRoles = context.User.FindFirst("realm_access")?.Value;
            if (realmRoles is not null && realmRoles.Contains("system-administrator"))
                return true;

            // Fallback: check custom claim or DB-backed role (via middleware)
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
app.UseAuthorization();

// Role enrichment middleware: adds app_role claim from DB for authorization policies
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

// Tenant context middleware: validates X-Tenant-Id header and sets TenantId in HttpContext.Items
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // Paths that don't require a tenant context
    var tenantExemptPaths = new[]
    {
        "/api/users/me",
        "/api/tenants",
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
        var isAdmin = context.User.HasClaim("app_role", "SystemAdministrator");
        var realmRoles = context.User.FindFirst("realm_access")?.Value;
        if (realmRoles is not null && realmRoles.Contains("system-administrator"))
            isAdmin = true;

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
