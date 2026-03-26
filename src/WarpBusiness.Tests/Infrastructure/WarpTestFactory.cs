using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WarpBusiness.Api.Data;

namespace WarpBusiness.Tests.Infrastructure;

public class WarpTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove ALL descriptors that reference ApplicationDbContext (options, configurations, context itself)
            // This covers DbContextOptions<T>, IDbContextOptionsConfiguration<T>, and the context registration
            var dbDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Any(t => t == typeof(ApplicationDbContext))))
                .ToList();

            foreach (var descriptor in dbDescriptors)
                services.Remove(descriptor);

            // Generate the DB name once so all requests within this factory share the same in-memory store
            var dbName = "WarpTestDb-" + Guid.NewGuid();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
        });

        // Set test environment name to prevent migrations from running
        builder.UseEnvironment("Test");
        builder.UseSetting("Jwt:Key", "test-secret-key-32-chars-minimum!!");
        builder.UseSetting("Jwt:Issuer", "WarpBusiness.Api.Test");
        builder.UseSetting("Jwt:Audience", "WarpBusiness.Web.Test");
        builder.UseSetting("AuthProvider:ActiveProvider", "Local");
    }
}
