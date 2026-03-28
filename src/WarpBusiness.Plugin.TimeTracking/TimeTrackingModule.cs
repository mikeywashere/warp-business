using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.TimeTracking.Data;
using WarpBusiness.Plugin.TimeTracking.Services;

namespace WarpBusiness.Plugin.TimeTracking;

public class TimeTrackingModule : ICustomModule
{
    public string ModuleId => "com.warpbusiness.timetracking";
    public string DisplayName => "Time Tracking";
    public string Version => "1.0.0";
    public string Description => "Time tracking — employee hours, pay rates, and customer billing.";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("warpbusiness")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("A connection string (warpbusiness or DefaultConnection) is required for the Time Tracking plugin.");

        services.AddDbContext<TimeTrackingDbContext>(options => options.UseNpgsql(connStr,
            npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null)));
        services.AddScoped<ITimeEntryTypeService, TimeEntryTypeService>();
        services.AddScoped<IEmployeePayRateService, EmployeePayRateService>();
        services.AddScoped<ICustomerBillingRateService, CustomerBillingRateService>();
        services.AddScoped<ITimeEntryService, TimeEntryService>();
    }

    public void Configure(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TimeTrackingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TimeTrackingModule>>();
        const int maxRetries = 10;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                db.Database.Migrate();
                logger.LogInformation("Time Tracking plugin: database migration applied.");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 5)));
                logger.LogWarning(ex, "Time Tracking plugin: migration attempt {Attempt}/{Max} failed. Retrying in {Delay}s...",
                    attempt, maxRetries, delay.TotalSeconds);
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Time Tracking plugin: migration failed after {Max} attempts.", maxRetries);
            }
        }
    }

    public IEnumerable<ModuleNavItem> GetNavItems() =>
    [
        new ModuleNavItem("Time Tracking", "timetracking", "bi-clock", DisplayOrder: 60),
    ];

    public IEnumerable<Assembly> GetBlazorAssemblies() =>
        [Assembly.GetExecutingAssembly()];
}
