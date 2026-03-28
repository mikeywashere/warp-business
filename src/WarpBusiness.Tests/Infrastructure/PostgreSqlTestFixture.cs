using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Crm.Data;

namespace WarpBusiness.Tests.Infrastructure;

/// <summary>
/// Provides a PostgreSQL container via Testcontainers for integration tests that need real database constraints.
/// Use as IClassFixture&lt;PostgreSqlTestFixture&gt; in test classes that require validating FK behavior, unique indexes, etc.
/// </summary>
public class PostgreSqlTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>Gets the PostgreSQL connection string once the container is started.</summary>
    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

    public async Task InitializeAsync()
    {
        // Build and start a PostgreSQL container
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithPassword("test_password")
            .WithDatabase("warp_test_db")
            .Build();

        await _container.StartAsync();

        // Run CRM migrations to set up the schema
        await using var context = CreateCrmDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a new CrmDbContext instance connected to the test container.
    /// Caller is responsible for disposing the context.
    /// </summary>
    public CrmDbContext CreateCrmDbContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new CrmDbContext(options, NullTenantContext.Instance);
    }
}
