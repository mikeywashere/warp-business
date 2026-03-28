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

        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connStr));
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
        try
        {
            db.Database.Migrate();
            logger.LogInformation("Catalog plugin: database migration applied.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalog plugin: migration failed.");
        }
    }

    public IEnumerable<ModuleNavItem> GetNavItems() =>
    [
        new ModuleNavItem("Catalog", "catalog", "bi-box-seam", DisplayOrder: 40),
    ];

    public IEnumerable<Assembly> GetBlazorAssemblies() =>
        [Assembly.GetExecutingAssembly()];
}
