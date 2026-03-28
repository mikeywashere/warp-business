using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Catalog.Data;
using WarpBusiness.Plugin.Catalog.Services;

namespace WarpBusiness.Plugin.Catalog;

public class CatalogModule : ICustomModule
{
    public string ModuleId => "com.warpbusiness.catalog";
    public string DisplayName => "Catalog";
    public string Version => "1.0.0";
    public string Description => "Product catalog — categories, products, variants, images, and ingredients.";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("warpbusiness")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("A connection string (warpbusiness or DefaultConnection) is required for the Catalog plugin.");

        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connStr,
            npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null)));
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductImageService, ProductImageService>();
        services.AddScoped<IProductIngredientService, ProductIngredientService>();
        services.AddScoped<IProductVariantService, ProductVariantService>();
    }

    public void Configure(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogModule>>();
        const int maxRetries = 10;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                db.Database.Migrate();
                logger.LogInformation("Catalog plugin: database migration applied.");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 5)));
                logger.LogWarning(ex, "Catalog plugin: migration attempt {Attempt}/{Max} failed. Retrying in {Delay}s...",
                    attempt, maxRetries, delay.TotalSeconds);
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Catalog plugin: migration failed after {Max} attempts.", maxRetries);
            }
        }
    }

    public IEnumerable<ModuleNavItem> GetNavItems() =>
    [
        new ModuleNavItem("Catalog", "catalog", "bi-box-seam", DisplayOrder: 40),
    ];

    public IEnumerable<Assembly> GetBlazorAssemblies() =>
        [Assembly.GetExecutingAssembly()];
}
