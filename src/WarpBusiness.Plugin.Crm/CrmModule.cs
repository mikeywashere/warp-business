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

        services.AddDbContext<CrmDbContext>(options => options.UseNpgsql(connStr));
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
        try
        {
            db.Database.Migrate();
            logger.LogInformation("CRM plugin: database migration applied.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CRM plugin: migration failed.");
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
