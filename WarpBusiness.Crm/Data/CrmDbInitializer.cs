using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WarpBusiness.Crm.Data;

public class CrmDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CrmDbInitializer> _logger;

    public CrmDbInitializer(IServiceProvider serviceProvider, ILogger<CrmDbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        _logger.LogInformation("Applying CRM database migrations...");
        await dbContext.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("CRM database migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
