using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Identity;
using WarpBusiness.Api.Identity.Tenancy;
using WarpBusiness.Api.Middleware;
using WarpBusiness.Api.Plugins;
using WarpBusiness.Plugin.Catalog;
using WarpBusiness.Plugin.Crm;
using WarpBusiness.Plugin.TimeTracking;
using WarpBusiness.Plugin.Invoicing;
using WarpBusiness.Plugin.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("warpbusiness"),
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 6,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null)));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Multi-provider authentication (Local, Keycloak, Microsoft)
builder.Services.AddWarpAuthentication(builder.Configuration);
builder.Services.AddScoped<IExternalIdentityMapper, ExternalIdentityMapper>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Tenant context — resolved from JWT tenant_id claim
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, JwtTenantContext>();
builder.Services.AddScoped<WarpBusiness.Api.Services.ITenantSamlService, WarpBusiness.Api.Services.TenantSamlService>();
builder.Services.AddScoped<IClaimsTransformation, TenantClaimsTransformation>();

// Authorization with tenant-aware policies
builder.Services.AddAuthorization(options =>
{
    // Require an active tenant for all data (CRM, EmployeeManagement)
    options.AddPolicy("RequireActiveTenant", policy =>
        policy.RequireClaim("tenant_id"));

    // Require TenantAdmin role within the active tenant
    options.AddPolicy("RequireTenantAdmin", policy =>
        policy.RequireClaim("tenant_id")
              .RequireClaim("tenant_role", "TenantAdmin"));
});

// Plugin/module discovery — must happen before AddControllers
var pluginsDir = Path.Combine(builder.Environment.ContentRootPath, "plugins");
var crmModule = new CrmModule();
var employeeModule = new WarpBusiness.Plugin.EmployeeManagement.EmployeeManagementModule();
var catalogModule = new CatalogModule();
var timeTrackingModule = new TimeTrackingModule();
var invoicingModule = new InvoicingModule();
builder.Services.AddCustomModules(
    builder.Configuration,
    pluginsDir,
    firstPartyModules: new ICustomModule[] { crmModule, employeeModule, catalogModule, timeTrackingModule, invoicingModule });

builder.Services.AddControllers()
    .AddApplicationPart(typeof(CrmModule).Assembly)
    .AddApplicationPart(typeof(WarpBusiness.Plugin.EmployeeManagement.EmployeeManagementModule).Assembly)
    .AddApplicationPart(typeof(CatalogModule).Assembly)
    .AddApplicationPart(typeof(TimeTrackingModule).Assembly)
    .AddApplicationPart(typeof(InvoicingModule).Assembly);
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply EF migrations automatically in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var migrationLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EFMigrations");
    const int maxRetries = 10;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            migrationLogger.LogInformation("ApplicationDbContext migrations applied successfully.");
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 5)));
            migrationLogger.LogWarning(ex, "Migration attempt {Attempt}/{Max} failed. Retrying in {Delay}s...",
                attempt, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsEnvironment("Test"))
    app.UseHttpsRedirection();
app.UseTenantResolution();
app.UseAuthentication();
app.UseAuthorization();
app.UseCustomModules();
app.MapControllers();

// Seed roles and admin user on startup
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Manager", "User", "TenantAdmin" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Seed default admin user (idempotent — skips if already exists).
    // Skipped in Test environment to keep test isolation clean.
    if (!app.Environment.IsEnvironment("Test"))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string adminEmail = "mikenging@hotmail.com";
        const string adminPassword = "WooHoo";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Michael",
                LastName = "Schmidt",
                AuthProvider = "Local",
            };

            // Create without password first, then set hash directly.
            // "WooHoo" is intentionally a weak seed password — it bypasses the
            // configured password policy because the user MUST change it on
            // first Keycloak login (required action: UPDATE_PASSWORD).
            var result = await userManager.CreateAsync(adminUser);
            if (result.Succeeded)
            {
                adminUser.PasswordHash = userManager.PasswordHasher.HashPassword(adminUser, adminPassword);
                await userManager.UpdateAsync(adminUser);
                await userManager.AddToRoleAsync(adminUser, "Admin");

                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("AdminSeed");
                logger.LogInformation("Seeded admin user {Email} — password must be changed on first login", adminEmail);
            }
        }
    }
}

app.Run();

public partial class Program { }
