using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace WarpBusiness.Catalog.Data;

public class CatalogDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CatalogDbInitializer> _logger;

    public CatalogDbInitializer(IServiceProvider serviceProvider, ILogger<CatalogDbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        _logger.LogInformation("Applying Catalog database migrations...");

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDuplicateTableError(ex))
        {
            // Tables already exist without migration tracking (e.g., from a prior EnsureCreatedAsync).
            // Record all pending migrations as applied so future migrations can run incrementally.
            _logger.LogWarning("Catalog schema tables already exist without migration history. Reconciling...");
            await ReconcileMigrationHistoryAsync(dbContext, cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        _logger.LogInformation("Catalog database migrations applied.");
    }

    private static bool IsDuplicateTableError(Exception ex)
    {
        var pgEx = ex as PostgresException ?? ex.InnerException as PostgresException;
        return pgEx?.SqlState == "42P07"; // duplicate_table
    }

    private async Task ReconcileMigrationHistoryAsync(CatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        // Ensure __EFMigrationsHistory table exists before we try to read it
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            )
            """, cancellationToken);

        var applied = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet();
        foreach (var migrationId in dbContext.Database.GetMigrations().Where(m => !applied.Contains(m)))
        {
            _logger.LogWarning("Marking Catalog migration {MigrationId} as applied (tables already exist).", migrationId);
            // migrationId comes from compiled assembly — not user input, safe to interpolate
#pragma warning disable EF1002
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{migrationId}', '10.0.5')
                ON CONFLICT DO NOTHING
                """,
                cancellationToken);
#pragma warning restore EF1002
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
