using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WarpBusiness.Taxonomy.Data;

public class TaxonomyDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaxonomyDbInitializer> _logger;

    public TaxonomyDbInitializer(IServiceProvider serviceProvider, ILogger<TaxonomyDbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaxonomyDbContext>();

        _logger.LogInformation("Applying Taxonomy database migrations...");
        await dbContext.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Taxonomy database migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
