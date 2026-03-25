using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WarpBusiness.Api.Data;

/// <summary>
/// Provides a design-time DbContext for EF Core tooling (migrations, scaffolding).
/// Uses a local PostgreSQL connection that does NOT require Aspire to be running.
/// Override via environment variable: ConnectionStrings__warpbusiness
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__warpbusiness")
            ?? "Host=localhost;Port=5432;Database=warpbusiness_dev;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsAssembly("WarpBusiness.Api"));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
