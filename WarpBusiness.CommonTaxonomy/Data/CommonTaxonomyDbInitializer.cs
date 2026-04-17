using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using WarpBusiness.CommonTaxonomy.Models;

namespace WarpBusiness.CommonTaxonomy.Data;

public class CommonTaxonomyDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommonTaxonomyDbInitializer> _logger;

    public CommonTaxonomyDbInitializer(IServiceProvider serviceProvider, ILogger<CommonTaxonomyDbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommonTaxonomyDbContext>();

        _logger.LogInformation("Applying common_taxonomy database migrations...");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE SCHEMA IF NOT EXISTS common_taxonomy", cancellationToken);

        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDuplicateTableError(ex))
        {
            _logger.LogWarning("common_taxonomy tables already exist without migration history. Reconciling...");
            await ReconcileMigrationHistoryAsync(db, cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        }

        _logger.LogInformation("common_taxonomy migrations applied.");

        var providers = new[]
        {
            new { Key = "google", Name = "Google Shopping", ApiDownload = true, FileImport = false },
            new { Key = "amazon", Name = "Amazon Browse Tree", ApiDownload = false, FileImport = true },
            new { Key = "ebay", Name = "eBay Category Tree", ApiDownload = true, FileImport = false },
            new { Key = "etsy", Name = "Etsy Taxonomy", ApiDownload = true, FileImport = false },
            new { Key = "newegg", Name = "Newegg Category Tree", ApiDownload = false, FileImport = true },
        };

        foreach (var p in providers)
        {
            if (!await db.Providers.AnyAsync(x => x.Key == p.Key, cancellationToken))
            {
                db.Providers.Add(new TaxonomyProvider
                {
                    Id = Guid.NewGuid(),
                    Key = p.Key,
                    DisplayName = p.Name,
                    SupportsApiDownload = p.ApiDownload,
                    SupportsFileImport = p.FileImport,
                    IsActive = true
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDuplicateTableError(Exception ex)
    {
        var pgEx = ex as PostgresException ?? ex.InnerException as PostgresException;
        return pgEx?.SqlState == "42P07";
    }

    private async Task ReconcileMigrationHistoryAsync(CommonTaxonomyDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS common_taxonomy."__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            )
            """, cancellationToken);

        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet();
        foreach (var migrationId in db.Database.GetMigrations().Where(m => !applied.Contains(m)))
        {
            _logger.LogWarning("Marking CommonTaxonomy migration {MigrationId} as applied.", migrationId);
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO common_taxonomy."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{migrationId}', '10.0.5')
                ON CONFLICT DO NOTHING
                """,
                cancellationToken);
#pragma warning restore EF1002
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
