using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        _logger.LogInformation("Initializing common_taxonomy schema...");

        // EnsureCreatedAsync won't create tables in a non-default schema on a shared database
        // unless the schema already exists. Create it explicitly first.
        await db.Database.ExecuteSqlRawAsync(
            "CREATE SCHEMA IF NOT EXISTS common_taxonomy", cancellationToken);

        await db.Database.EnsureCreatedAsync(cancellationToken);

        _logger.LogInformation("common_taxonomy schema initialized.");

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
