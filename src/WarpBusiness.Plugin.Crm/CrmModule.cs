using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Crm.Data;
using WarpBusiness.Plugin.Crm.Services;

namespace WarpBusiness.Plugin.Crm;

public class CrmModule : ICustomModule
{
    public string ModuleId => "com.warpbusiness.crm";
    public string DisplayName => "CRM";
    public string Version => "1.0.0";
    public string Description => "Customer Relationship Management — contacts, companies, deals, activities, and custom fields.";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("warpbusiness")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("A connection string (warpbusiness or DefaultConnection) is required for the CRM plugin.");

        services.AddDbContext<CrmDbContext>(options => options.UseNpgsql(connStr,
            npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null)));
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IContactEmployeeRelationshipService, ContactEmployeeRelationshipService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IDealService, DealService>();
        services.AddScoped<IActivityService, ActivityService>();
    }

    public void Configure(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CrmModule>>();
        const int maxRetries = 10;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                db.Database.Migrate();
                logger.LogInformation("CRM plugin: database migration applied.");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 5)));
                logger.LogWarning(ex, "CRM plugin: migration attempt {Attempt}/{Max} failed. Retrying in {Delay}s...",
                    attempt, maxRetries, delay.TotalSeconds);
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CRM plugin: migration failed after {Max} attempts.", maxRetries);
            }
        }
    }

    public IEnumerable<ModuleNavItem> GetNavItems() =>
    [
        new ModuleNavItem("Contacts",  "contacts",  "bi-person-lines-fill", DisplayOrder: 10),
        new ModuleNavItem("Companies", "companies", "bi-building",          DisplayOrder: 20),
        new ModuleNavItem("Deals",     "deals",     "bi-currency-dollar",   DisplayOrder: 30),
    ];

    public IEnumerable<Assembly> GetBlazorAssemblies() =>
        [Assembly.GetExecutingAssembly()];
}
