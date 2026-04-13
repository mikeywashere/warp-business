using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Models;

namespace WarpBusiness.Api.Data;

public class DbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(IServiceProvider serviceProvider, ILogger<DbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WarpBusinessDbContext>();

        _logger.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Database migrations applied.");

        await SeedDataAsync(dbContext, cancellationToken);
        await SeedCurrenciesAsync(dbContext, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedDataAsync(WarpBusinessDbContext dbContext, CancellationToken cancellationToken)
    {
        const string adminEmail = "mikenging@hotmail.com";
        const string defaultTenantSlug = "warp";

        // Seed default tenant
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Slug == defaultTenantSlug, cancellationToken);

        if (tenant is null)
        {
            _logger.LogInformation("Seeding default tenant: Warp");
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Warp",
                Slug = defaultTenantSlug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Default tenant seeded successfully.");
        }

        // Seed admin user
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == adminEmail, cancellationToken);

        if (existingUser is null)
        {
            _logger.LogInformation("Seeding System Administrator: Michael Schmidt ({Email})", adminEmail);

            existingUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                FirstName = "Michael",
                LastName = "Schmidt",
                Email = adminEmail,
                Role = UserRole.SystemAdministrator,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Users.Add(existingUser);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("System Administrator seeded successfully.");
        }
        else
        {
            _logger.LogInformation("System Administrator already exists, skipping seed.");
        }

        // Seed admin membership in default tenant
        var membership = await dbContext.UserTenantMemberships
            .FirstOrDefaultAsync(m => m.UserId == existingUser.Id && m.TenantId == tenant.Id, cancellationToken);

        if (membership is null)
        {
            _logger.LogInformation("Adding System Administrator to default tenant.");
            dbContext.UserTenantMemberships.Add(new UserTenantMembership
            {
                UserId = existingUser.Id,
                TenantId = tenant.Id,
                JoinedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedCurrenciesAsync(WarpBusinessDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingCount = await dbContext.Currencies.CountAsync(cancellationToken);
        if (existingCount > 0)
        {
            _logger.LogInformation("Currencies already seeded ({Count} found), skipping.", existingCount);
            return;
        }

        _logger.LogInformation("Seeding ISO 4217 currencies...");
        var currencies = CurrencySeedData.GetAllCurrencies();
        dbContext.Currencies.AddRange(currencies);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} currencies.", currencies.Count);
    }
}
