using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Data;

public class SchedulingDbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchedulingDbInitializer> _logger;

    public SchedulingDbInitializer(IServiceProvider serviceProvider, ILogger<SchedulingDbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();

        _logger.LogInformation("Applying scheduling database migrations...");
        await db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Scheduling database migrations applied.");

        await SeedBreakRulesAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedBreakRulesAsync(SchedulingDbContext db, CancellationToken cancellationToken)
    {
        var hasRules = await db.BreakRules.AnyAsync(r => r.State == "WA", cancellationToken);
        if (hasRules)
            return;

        _logger.LogInformation("Seeding break rules for WA...");

        var rules = new List<BreakRule>
        {
            // WA Rest Break: 10 min paid per 4 hours worked, max 3 consecutive hours without break
            new()
            {
                Id = Guid.NewGuid(),
                State = "WA",
                RuleType = BreakRuleType.Rest,
                MinShiftMinutesToTrigger = 0,
                BreakDurationMinutes = 10,
                IsPaid = true,
                FrequencyMinutes = 240,            // 1 break per 4 hours
                MaxConsecutiveMinutesWithoutBreak = 180, // cannot work more than 3 consecutive hours
                IsWaivable = false,
                CountsAsHoursWorked = true,
                Notes = "WA State: 10 min paid rest break per 4 hrs worked. Cannot work more than 3 consecutive hours without break. Must be scheduled near midpoint of work period. Employee may not waive."
            },
            // WA Meal Break: 30 min when working > 5 hours, must start between 2nd and 5th hour
            new()
            {
                Id = Guid.NewGuid(),
                State = "WA",
                RuleType = BreakRuleType.Meal,
                MinShiftMinutesToTrigger = 301,    // triggered if shift > 5 hours
                BreakDurationMinutes = 30,
                IsPaid = false,
                MustStartAfterShiftMinutes = 120,  // must start after 2nd hour
                MustStartBeforeShiftMinutes = 300, // must start before 5th hour
                IsWaivable = true,
                CountsAsHoursWorked = false,
                Notes = "WA State: 30 min meal break when shift > 5 hrs. Must start between 2nd and 5th hour. Waivable by mutual agreement. Paid if employee required to remain on duty or on-call."
            },
            // WA Second Meal Break: required if working > 11 hours
            new()
            {
                Id = Guid.NewGuid(),
                State = "WA",
                RuleType = BreakRuleType.Meal,
                MinShiftMinutesToTrigger = 661,    // triggered if shift > 11 hours
                BreakDurationMinutes = 30,
                IsPaid = false,
                AdditionalBreakAfterMinutes = 660, // second meal if shift > 11 hours
                IsWaivable = true,
                CountsAsHoursWorked = false,
                Notes = "WA State: second 30 min meal break required if working more than 11 hours in a day."
            },
            // WA Overtime Meal Break: extra meal if working > 3 hours past scheduled end
            new()
            {
                Id = Guid.NewGuid(),
                State = "WA",
                RuleType = BreakRuleType.Meal,
                MinShiftMinutesToTrigger = 0,
                BreakDurationMinutes = 30,
                IsPaid = false,
                OvertimeExtraBreakAfterMinutes = 180, // extra meal if > 3 hours past scheduled end
                IsWaivable = true,
                CountsAsHoursWorked = false,
                Notes = "WA State: additional 30 min meal break if employee works more than 3 hours beyond scheduled shift end."
            }
        };

        db.BreakRules.AddRange(rules);
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} WA break rules.", rules.Count);
    }
}
