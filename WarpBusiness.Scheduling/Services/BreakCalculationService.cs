using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Services;

public record RequiredBreak(BreakRuleType Type, bool IsPaid, int DurationMinutes, TimeOnly? LatestStartTime);

public class BreakCalculationService
{
    private readonly SchedulingDbContext _db;

    public BreakCalculationService(SchedulingDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Calculates the required breaks for a shift at a given location state.
    /// Returns a list of breaks that must be scheduled.
    /// </summary>
    public async Task<IReadOnlyList<RequiredBreak>> CalculateRequiredBreaksAsync(
        TimeOnly scheduledStart,
        TimeOnly scheduledEnd,
        string state,
        CancellationToken cancellationToken = default)
    {
        var shiftMinutes = CalculateShiftMinutes(scheduledStart, scheduledEnd);
        var rules = await _db.BreakRules
            .Where(r => r.State == state)
            .ToListAsync(cancellationToken);

        var required = new List<RequiredBreak>();

        var restRules = rules.Where(r => r.RuleType == BreakRuleType.Rest && shiftMinutes >= r.MinShiftMinutesToTrigger).ToList();
        foreach (var rule in restRules)
        {
            if (rule.OvertimeExtraBreakAfterMinutes.HasValue)
                continue; // overtime rules handled separately

            if (rule.FrequencyMinutes.HasValue)
            {
                var count = shiftMinutes / rule.FrequencyMinutes.Value;
                for (int i = 1; i <= count; i++)
                {
                    // place break near midpoint of each work period
                    var periodMidpoint = scheduledStart.AddMinutes(rule.FrequencyMinutes.Value * i - rule.FrequencyMinutes.Value / 2);
                    required.Add(new RequiredBreak(BreakRuleType.Rest, rule.IsPaid, rule.BreakDurationMinutes, periodMidpoint));
                }
            }
            else
            {
                required.Add(new RequiredBreak(BreakRuleType.Rest, rule.IsPaid, rule.BreakDurationMinutes, null));
            }
        }

        var mealRules = rules.Where(r => r.RuleType == BreakRuleType.Meal
            && shiftMinutes >= r.MinShiftMinutesToTrigger
            && !r.OvertimeExtraBreakAfterMinutes.HasValue).ToList();

        foreach (var rule in mealRules)
        {
            TimeOnly? latestStart = rule.MustStartBeforeShiftMinutes.HasValue
                ? scheduledStart.AddMinutes(rule.MustStartBeforeShiftMinutes.Value)
                : null;
            required.Add(new RequiredBreak(BreakRuleType.Meal, rule.IsPaid, rule.BreakDurationMinutes, latestStart));
        }

        return required;
    }

    private static int CalculateShiftMinutes(TimeOnly start, TimeOnly end)
    {
        var minutes = (end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute);
        return minutes < 0 ? minutes + 24 * 60 : minutes;
    }
}
