using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Employees.Data;
using WarpBusiness.Scheduling.Data;

namespace WarpBusiness.Api.Endpoints;

public static class ShiftReplacementEndpoints
{
    public static void MapShiftReplacementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/schedules")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/{scheduleId:guid}/shifts/{shiftId:guid}/replacements", GetReplacements);
    }

    private static async Task<IResult> GetReplacements(
        Guid scheduleId, Guid shiftId,
        HttpContext context,
        SchedulingDbContext schedDb,
        EmployeeDbContext empDb)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var schedule = await schedDb.Schedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.TenantId == tenantId);
        if (schedule is null)
            return Results.NotFound();

        var shift = await schedDb.ScheduleShifts
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.ScheduleId == scheduleId);
        if (shift is null)
            return Results.NotFound();

        var shiftDurationHours = (decimal)(shift.ScheduledEndTime - shift.ScheduledStartTime).TotalHours;

        // Week boundaries (Monday 00:00 – Sunday 23:59:59) containing the shift's date
        var dayOfWeek = (int)shift.Date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var weekStart = shift.Date.AddDays(-daysFromMonday);
        var weekEnd = weekStart.AddDays(6);

        // Employees qualified for the shift's position in this tenant
        var qualifiedEmployeeIds = await schedDb.EmployeePositions
            .Where(ep => ep.PositionId == shift.PositionId && ep.TenantId == tenantId)
            .Select(ep => ep.EmployeeId)
            .ToListAsync();

        if (qualifiedEmployeeIds.Count == 0)
            return Results.Ok(Array.Empty<ReplacementCandidateResponse>());

        // All scheduled shifts for qualified employees during the week (across all schedules)
        var weeklyShifts = await schedDb.ScheduleShifts
            .Where(s => qualifiedEmployeeIds.Contains(s.EmployeeId)
                        && s.Date >= weekStart
                        && s.Date <= weekEnd)
            .ToListAsync();

        // Compute hours and detect time conflicts per candidate; exclude conflicting employees
        var candidates = qualifiedEmployeeIds
            .Select(empId =>
            {
                var empShifts = weeklyShifts.Where(s => s.EmployeeId == empId).ToList();

                var hoursThisWeek = (decimal)empShifts
                    .Sum(s => (s.ScheduledEndTime - s.ScheduledStartTime).TotalHours);

                // Conflict = existing shift on the same date whose time range overlaps this shift
                var hasConflict = empShifts.Any(s =>
                    s.Date == shift.Date &&
                    s.ScheduledStartTime < shift.ScheduledEndTime &&
                    s.ScheduledEndTime > shift.ScheduledStartTime);

                return (EmployeeId: empId, HoursThisWeek: hoursThisWeek, HasConflict: hasConflict);
            })
            .Where(c => !c.HasConflict)
            .ToList();

        if (candidates.Count == 0)
            return Results.Ok(Array.Empty<ReplacementCandidateResponse>());

        // Resolve employee details from the separate EmployeeDbContext
        var candidateIds = candidates.Select(c => c.EmployeeId).ToList();
        var employees = await empDb.Employees
            .Where(e => e.TenantId == tenantId && candidateIds.Contains(e.Id))
            .ToListAsync();

        var employeeMap = employees.ToDictionary(e => e.Id);

        const decimal overtimeThreshold = 40m;

        var results = candidates
            .Where(c => employeeMap.ContainsKey(c.EmployeeId))
            .Select(c =>
            {
                var emp = employeeMap[c.EmployeeId];
                var hoursRemaining = Math.Max(0m, overtimeThreshold - c.HoursThisWeek);
                var wouldCauseOvertime = c.HoursThisWeek + shiftDurationHours > overtimeThreshold;
                return new ReplacementCandidateResponse(
                    emp.Id,
                    emp.EmployeeNumber,
                    emp.FirstName,
                    emp.LastName,
                    c.HoursThisWeek,
                    hoursRemaining,
                    wouldCauseOvertime,
                    HasConflict: false);
            })
            .OrderBy(r => r.HoursScheduledThisWeek)
            .ToList();

        return Results.Ok(results);
    }
}

public record ReplacementCandidateResponse(
    Guid EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    decimal HoursScheduledThisWeek,
    decimal HoursRemainingBeforeOvertime,
    bool WouldCauseOvertime,
    bool HasConflict);
