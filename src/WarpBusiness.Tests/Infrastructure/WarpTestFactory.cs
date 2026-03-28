using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WarpBusiness.Api.Data;
using WarpBusiness.Plugin.Catalog.Data;
using WarpBusiness.Plugin.Crm.Data;
using WarpBusiness.Plugin.EmployeeManagement.Data;
using WarpBusiness.Plugin.Invoicing.Data;
using WarpBusiness.Plugin.TimeTracking.Data;

namespace WarpBusiness.Tests.Infrastructure;

public class WarpTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Provide a dummy connection string so plugins don't throw during ConfigureServices.
        // The real DbContexts are swapped for in-memory below.
        builder.UseSetting("ConnectionStrings:warpbusiness", "Host=localhost;Database=test_placeholder");

        builder.ConfigureServices(services =>
        {
            // Replace all PostgreSQL-backed DbContexts with isolated in-memory databases
            ReplaceWithInMemory<ApplicationDbContext>(services, "WarpTestDb-" + Guid.NewGuid());
            ReplaceWithInMemory<CrmDbContext>(services, "WarpCrmTestDb-" + Guid.NewGuid());
            ReplaceWithInMemory<EmployeeDbContext>(services, "WarpEmpTestDb-" + Guid.NewGuid());
            ReplaceWithInMemory<CatalogDbContext>(services, "WarpCatalogTestDb-" + Guid.NewGuid());
            ReplaceWithInMemory<TimeTrackingDbContext>(services, "WarpTimeTrackingTestDb-" + Guid.NewGuid());
            ReplaceWithInMemory<InvoicingDbContext>(services, "WarpInvoicingTestDb-" + Guid.NewGuid());

        });

        // Set test environment name to prevent migrations from running
        builder.UseEnvironment("Test");
        builder.UseSetting("Jwt:Key", "test-secret-key-32-chars-minimum!!");
        builder.UseSetting("Jwt:Issuer", "WarpBusiness.Api.Test");
        builder.UseSetting("Jwt:Audience", "WarpBusiness.Web.Test");
        builder.UseSetting("AuthProvider:ActiveProvider", "Local");
    }

    private static void ReplaceWithInMemory<TContext>(IServiceCollection services, string dbName)
        where TContext : DbContext
    {
        // Remove DbContextOptions<T>, the context itself, and any IDbContextOptionsConfiguration<T>
        // so no Npgsql provider leaks through
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<TContext>) ||
                d.ServiceType == typeof(TContext) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericArguments().Any(t => t == typeof(TContext))))
            .ToList();

        foreach (var descriptor in toRemove)
            services.Remove(descriptor);

        services.AddDbContext<TContext>(options => options.UseInMemoryDatabase(dbName));
    }
}
