using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WarpBusiness.Employees.Data;

public class EmployeeDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmployeeDbInitializer> _logger;

    public EmployeeDbInitializer(IServiceProvider serviceProvider, ILogger<EmployeeDbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();

        _logger.LogInformation("Applying employee database migrations...");
        await dbContext.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Employee database migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
