using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WarpBusiness.Storage;

public class StorageBucketInitializer : IHostedService
{
    private readonly IFileStorageService _storageService;
    private readonly ILogger<StorageBucketInitializer> _logger;
    private readonly string[] _buckets = ["warp-catalog", "warp-logos"];

    public StorageBucketInitializer(IFileStorageService storageService, ILogger<StorageBucketInitializer> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring storage buckets exist...");

        foreach (var bucket in _buckets)
        {
            try
            {
                await _storageService.EnsureBucketExistsAsync(bucket, cancellationToken);
                _logger.LogInformation("Bucket '{Bucket}' is ready", bucket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure bucket '{Bucket}' exists", bucket);
                throw;
            }
        }

        _logger.LogInformation("Storage bucket initialization complete");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
