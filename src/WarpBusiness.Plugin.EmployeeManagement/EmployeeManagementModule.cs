using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.EmployeeManagement.Data;
using WarpBusiness.Plugin.EmployeeManagement.Services;

namespace WarpBusiness.Plugin.EmployeeManagement;

public class EmployeeManagementModule : ICustomModule
{
    public string ModuleId => "com.warpbusiness.employee-management";
    public string DisplayName => "Employee Management";
    public string Version => "1.0.0";
    public string Description => "Manage employees, departments, and org structure within the CRM.";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("warpbusiness")
            ?? throw new InvalidOperationException("warpbusiness connection string is required for the Employee Management plugin.");

        services.AddDbContext<EmployeeDbContext>(options =>
            options.UseNpgsql(connStr));

        services.AddScoped<IEmployeeService, EmployeeService>();
    }

    public void Configure(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EmployeeManagementModule>>();
        try
        {
            db.Database.Migrate();
            logger.LogInformation("Employee Management plugin: database migration applied.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Employee Management plugin: migration failed.");
        }
    }

    public IEnumerable<ModuleNavItem> GetNavItems() =>
    [
        new ModuleNavItem(
            Label: "Employees",
            Href: "employees",
            Icon: "bi-people",
            DisplayOrder: 50
        )
    ];

    public IEnumerable<Assembly> GetBlazorAssemblies() =>
        [Assembly.GetExecutingAssembly()];
}
