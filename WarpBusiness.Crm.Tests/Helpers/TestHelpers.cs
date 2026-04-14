using Microsoft.EntityFrameworkCore;
using WarpBusiness.Crm.Data;

namespace WarpBusiness.Crm.Tests.Helpers;

public static class TestHelpers
{
    /// <summary>
    /// Creates a CrmDbContext configured for PostgreSQL with the given connection string.
    /// </summary>
    public static CrmDbContext CreatePostgresCrmDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new CrmDbContext(options);
    }

    /// <summary>
    /// Creates a CrmDbContext using the InMemory provider for fast unit tests.
    /// </summary>
    public static CrmDbContext CreateInMemoryCrmDbContext(string? databaseName = null)
    {
        databaseName ??= Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new CrmDbContext(options);
    }

    /// <summary>
    /// Ensures the CRM schema tables exist using EnsureCreatedAsync.
    /// </summary>
    public static async Task EnsureCrmSchemaAsync(CrmDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
    }
}
