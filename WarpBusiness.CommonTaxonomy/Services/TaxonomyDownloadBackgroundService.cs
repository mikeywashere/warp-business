using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WarpBusiness.CommonTaxonomy.Services;

public class TaxonomyDownloadBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaxonomyDownloadBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(7);

    public TaxonomyDownloadBackgroundService(IServiceProvider serviceProvider, ILogger<TaxonomyDownloadBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<TaxonomyDownloadOrchestrator>();
                var results = await orchestrator.DownloadAllAsync(stoppingToken);
                foreach (var r in results)
                    _logger.LogInformation("Taxonomy download {Key}: {Status} ({Count} nodes)", r.ProviderKey, r.Success ? "OK" : "FAILED", r.NodesDownloaded);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Taxonomy background download cycle failed");
            }
            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
