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
            options.UseNpgsql(connStr,
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 6,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null)));

        services.AddScoped<IEmployeeService, EmployeeService>();
    }

    public void Configure(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EmployeeManagementModule>>();
        const int maxRetries = 10;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                db.Database.Migrate();
                logger.LogInformation("Employee Management plugin: database migration applied.");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 5)));
                logger.LogWarning(ex, "Employee Management plugin: migration attempt {Attempt}/{Max} failed. Retrying in {Delay}s...",
                    attempt, maxRetries, delay.TotalSeconds);
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Employee Management plugin: migration failed after {Max} attempts.", maxRetries);
            }
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
