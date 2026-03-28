using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Invoicing.Data;
using WarpBusiness.Plugin.Invoicing.Services;

namespace WarpBusiness.Plugin.Invoicing;

public class InvoicingModule : ICustomModule
{
    public string ModuleId => "com.warpbusiness.invoicing";
    public string DisplayName => "Invoicing";
    public string Version => "1.0.0";
    public string Description => "Invoicing — create, send, and track invoices with catalog and time tracking integration.";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("warpbusiness")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "A connection string (warpbusiness or DefaultConnection) is required for the Invoicing plugin.");

        services.AddDbContext<InvoicingDbContext>(options => options.UseNpgsql(connStr,
            npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null)));
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInvoiceLineItemService, InvoiceLineItemService>();
        services.AddScoped<IInvoicePaymentService, InvoicePaymentService>();
        services.AddScoped<IInvoiceSettingsService, InvoiceSettingsService>();
        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
    }

    public void Configure(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InvoicingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InvoicingModule>>();
        const int maxRetries = 10;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                db.Database.Migrate();
                logger.LogInformation("Invoicing plugin: database migration applied.");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 5)));
                logger.LogWarning(ex, "Invoicing plugin: migration attempt {Attempt}/{Max} failed. Retrying in {Delay}s...",
                    attempt, maxRetries, delay.TotalSeconds);
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invoicing plugin: migration failed after {Max} attempts.", maxRetries);
            }
        }
    }

    public IEnumerable<ModuleNavItem> GetNavItems() =>
    [
        new ModuleNavItem("Invoicing", "invoicing", "bi-file-earmark-text", DisplayOrder: 50),
    ];

    public IEnumerable<Assembly> GetBlazorAssemblies() =>
        [Assembly.GetExecutingAssembly()];
}
