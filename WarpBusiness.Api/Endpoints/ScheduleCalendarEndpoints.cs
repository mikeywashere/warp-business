using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Employees.Data;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Api.Endpoints;

public static class ScheduleCalendarEndpoints
{
    public static void MapScheduleCalendarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/calendar", GetCalendar);
    }

    private static async Task<IResult> GetCalendar(
        HttpContext context,
        SchedulingDbContext schedDb,
        EmployeeDbContext empDb,
        string? from = null,
        string? to = null)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (string.IsNullOrEmpty(from) || !DateOnly.TryParseExact(from, "yyyy-MM-dd", out var fromDate))
            return Results.BadRequest(new { message = "'from' query parameter is required and must be in yyyy-MM-dd format." });

        if (string.IsNullOrEmpty(to) || !DateOnly.TryParseExact(to, "yyyy-MM-dd", out var toDate))
            return Results.BadRequest(new { message = "'to' query parameter is required and must be in yyyy-MM-dd format." });

        if (toDate < fromDate)
            return Results.BadRequest(new { message = "'to' must be on or after 'from'." });

        if (toDate.DayNumber - fromDate.DayNumber > 90)
            return Results.BadRequest(new { message = "Date range cannot exceed 90 days." });

        // Load all shifts in range for this tenant via Schedule.TenantId
        var shifts = await schedDb.ScheduleShifts
            .Include(s => s.Schedule)
            .Where(s => s.Schedule.TenantId == tenantId && s.Date >= fromDate && s.Date <= toDate)
            .ToListAsync();

        if (shifts.Count == 0)
            return Results.Ok(Array.Empty<CalendarShiftResponse>());

        // Load positions for this tenant into a dictionary
        var positions = await schedDb.Positions
            .Where(p => p.TenantId == tenantId)
            .ToListAsync();
        var positionMap = positions.ToDictionary(p => p.Id);

        // Resolve employee details from the separate EmployeeDbContext
        var employeeIds = shifts.Select(s => s.EmployeeId).Distinct().ToList();
        var employees = await empDb.Employees
            .Where(e => e.TenantId == tenantId && employeeIds.Contains(e.Id))
            .ToListAsync();
        var employeeMap = employees.ToDictionary(e => e.Id);

        var results = shifts
            .Where(s => employeeMap.ContainsKey(s.EmployeeId) && positionMap.ContainsKey(s.PositionId))
            .Select(s =>
            {
                var emp = employeeMap[s.EmployeeId];
                var pos = positionMap[s.PositionId];
                return new CalendarShiftResponse(
                    s.Id,
                    s.ScheduleId,
                    s.Schedule.Name,
                    s.Date,
                    s.ScheduledStartTime,
                    s.ScheduledEndTime,
                    emp.Id,
                    emp.FirstName,
                    emp.LastName,
                    pos.Id,
                    pos.Name,
                    pos.Color,
                    s.Status,
                    s.Notes);
            })
            .OrderBy(r => r.Date).ThenBy(r => r.StartTime)
            .ToList();

        return Results.Ok(results);
    }
}

public record CalendarShiftResponse(
    Guid ShiftId,
    Guid ScheduleId,
    string ScheduleName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid EmployeeId,
    string EmployeeFirstName,
    string EmployeeLastName,
    Guid PositionId,
    string PositionName,
    string PositionColor,
    ShiftStatus Status,
    string? Notes);
