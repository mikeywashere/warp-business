using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Services;

public record BreakViolation(string Code, string Message, BreakRuleType RuleType);

public class BreakValidationService
{
    private readonly SchedulingDbContext _db;

    public BreakValidationService(SchedulingDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Validates the breaks assigned to a shift against the applicable state break rules.
    /// Returns a list of violations/warnings.
    /// </summary>
    public async Task<IReadOnlyList<BreakViolation>> ValidateAsync(
        Guid shiftId,
        string state,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.ScheduleShifts
            .Include(s => s.Breaks)
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken);

        if (shift is null)
            return [];

        var shiftMinutes = CalculateShiftMinutes(shift.ScheduledStartTime, shift.ScheduledEndTime);
        var rules = await _db.BreakRules
            .Where(r => r.State == state)
            .ToListAsync(cancellationToken);

        var violations = new List<BreakViolation>();

        var takenRestBreaks = shift.Breaks.Where(b => b.BreakType == BreakType.Rest).ToList();
        var takenMealBreaks = shift.Breaks.Where(b => b.BreakType == BreakType.Meal).ToList();

        foreach (var rule in rules.Where(r => r.RuleType == BreakRuleType.Rest
            && shiftMinutes >= r.MinShiftMinutesToTrigger
            && r.FrequencyMinutes.HasValue))
        {
            var required = shiftMinutes / rule.FrequencyMinutes!.Value;
            if (takenRestBreaks.Count < required)
            {
                violations.Add(new BreakViolation(
                    "REST_BREAK_MISSING",
                    $"Shift requires {required} rest break(s) per {rule.FrequencyMinutes / 60.0:0.#} hour(s) worked. Only {takenRestBreaks.Count} scheduled.",
                    BreakRuleType.Rest));
            }

            foreach (var restBreak in takenRestBreaks.Where(b => b.ScheduledStartTime.HasValue && b.ScheduledEndTime.HasValue))
            {
                var duration = CalculateBreakMinutes(restBreak.ScheduledStartTime!.Value, restBreak.ScheduledEndTime!.Value);
                if (duration < rule.BreakDurationMinutes)
                {
                    violations.Add(new BreakViolation(
                        "REST_BREAK_TOO_SHORT",
                        $"Rest break must be at least {rule.BreakDurationMinutes} minutes. Scheduled break is {duration} minutes.",
                        BreakRuleType.Rest));
                }
            }
        }

        foreach (var rule in rules.Where(r => r.RuleType == BreakRuleType.Meal
            && shiftMinutes >= r.MinShiftMinutesToTrigger
            && !r.OvertimeExtraBreakAfterMinutes.HasValue))
        {
            var mealBreaksForRule = takenMealBreaks.Count;
            var requiredMeals = rule.AdditionalBreakAfterMinutes.HasValue
                ? (shiftMinutes > rule.AdditionalBreakAfterMinutes.Value ? 2 : 1)
                : 1;

            if (mealBreaksForRule < requiredMeals)
            {
                violations.Add(new BreakViolation(
                    "MEAL_BREAK_MISSING",
                    $"Shift of {shiftMinutes / 60.0:0.#} hours requires {requiredMeals} meal break(s). Only {mealBreaksForRule} scheduled.",
                    BreakRuleType.Meal));
            }

            foreach (var mealBreak in takenMealBreaks.Where(b => b.ScheduledStartTime.HasValue))
            {
                var breakStartMinutes = (mealBreak.ScheduledStartTime!.Value.Hour * 60 + mealBreak.ScheduledStartTime.Value.Minute)
                    - (shift.ScheduledStartTime.Hour * 60 + shift.ScheduledStartTime.Minute);

                if (rule.MustStartAfterShiftMinutes.HasValue && breakStartMinutes < rule.MustStartAfterShiftMinutes.Value)
                {
                    violations.Add(new BreakViolation(
                        "MEAL_BREAK_TOO_EARLY",
                        $"Meal break must start no earlier than {rule.MustStartAfterShiftMinutes / 60.0:0.#} hours into shift.",
                        BreakRuleType.Meal));
                }

                if (rule.MustStartBeforeShiftMinutes.HasValue && breakStartMinutes > rule.MustStartBeforeShiftMinutes.Value)
                {
                    violations.Add(new BreakViolation(
                        "MEAL_BREAK_TOO_LATE",
                        $"Meal break must start no later than {rule.MustStartBeforeShiftMinutes / 60.0:0.#} hours into shift.",
                        BreakRuleType.Meal));
                }

                if (mealBreak.ScheduledEndTime.HasValue)
                {
                    var duration = CalculateBreakMinutes(mealBreak.ScheduledStartTime.Value, mealBreak.ScheduledEndTime.Value);
                    if (duration < rule.BreakDurationMinutes)
                    {
                        violations.Add(new BreakViolation(
                            "MEAL_BREAK_TOO_SHORT",
                            $"Meal break must be at least {rule.BreakDurationMinutes} minutes. Scheduled break is {duration} minutes.",
                            BreakRuleType.Meal));
                    }
                }
            }
        }

        return violations;
    }

    private static int CalculateShiftMinutes(TimeOnly start, TimeOnly end)
    {
        var minutes = (end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute);
        return minutes < 0 ? minutes + 24 * 60 : minutes;
    }

    private static int CalculateBreakMinutes(TimeOnly start, TimeOnly end)
    {
        var minutes = (end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute);
        return minutes < 0 ? minutes + 24 * 60 : minutes;
    }
}
