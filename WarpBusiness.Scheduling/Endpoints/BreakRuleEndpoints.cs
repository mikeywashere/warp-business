using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Endpoints;

public static class BreakRuleEndpoints
{
    public static void MapBreakRuleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/break-rules")
            .RequireAuthorization();

        group.MapGet("/{state}", GetByState);
        group.MapGet("/", GetAll);
    }

    private static async Task<IResult> GetByState(string state, SchedulingDbContext db)
    {
        var rules = await db.BreakRules
            .Where(r => r.State.ToUpperInvariant() == state.ToUpperInvariant())
            .OrderBy(r => r.RuleType)
            .ThenBy(r => r.MinShiftMinutesToTrigger)
            .ToListAsync();

        return Results.Ok(rules.Select(ToResponse));
    }

    private static async Task<IResult> GetAll(SchedulingDbContext db)
    {
        var rules = await db.BreakRules
            .OrderBy(r => r.State)
            .ThenBy(r => r.RuleType)
            .ThenBy(r => r.MinShiftMinutesToTrigger)
            .ToListAsync();

        return Results.Ok(rules.Select(ToResponse));
    }

    private static BreakRuleResponse ToResponse(BreakRule r) => new(
        r.Id, r.State, r.RuleType, r.MinShiftMinutesToTrigger, r.BreakDurationMinutes, r.IsPaid,
        r.FrequencyMinutes, r.MaxConsecutiveMinutesWithoutBreak,
        r.MustStartAfterShiftMinutes, r.MustStartBeforeShiftMinutes,
        r.IsWaivable, r.CountsAsHoursWorked,
        r.AdditionalBreakAfterMinutes, r.OvertimeExtraBreakAfterMinutes,
        r.Notes);
}

public record BreakRuleResponse(
    Guid Id, string State, BreakRuleType RuleType,
    int MinShiftMinutesToTrigger, int BreakDurationMinutes, bool IsPaid,
    int? FrequencyMinutes, int? MaxConsecutiveMinutesWithoutBreak,
    int? MustStartAfterShiftMinutes, int? MustStartBeforeShiftMinutes,
    bool IsWaivable, bool CountsAsHoursWorked,
    int? AdditionalBreakAfterMinutes, int? OvertimeExtraBreakAfterMinutes,
    string? Notes);
