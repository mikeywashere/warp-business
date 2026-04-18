using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Employees.Data;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Api.Endpoints;

public static class EmployeePortalEndpoints
{
    public static void MapEmployeePortalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/employee-portal/me")
            .RequireAuthorization("EmployeePortalAccess");

        group.MapGet("/", GetMe);
        group.MapGet("/schedule", GetSchedule);
        group.MapGet("/hours", GetHours);
        group.MapGet("/availability", GetAvailability);
        group.MapPut("/availability/{dayOfWeek:int}", UpsertAvailability);
        group.MapDelete("/availability/{dayOfWeek:int}", DeleteAvailability);
        group.MapGet("/time-off", GetTimeOff);
        group.MapPost("/time-off", RequestTimeOff);
        group.MapDelete("/time-off/{id:guid}", CancelTimeOff);
    }

    private static async Task<(IResult? error, WarpBusiness.Employees.Models.Employee? emp)> ResolveEmployee(
        HttpContext context, EmployeeDbContext empDb)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            return (Results.Forbid(), null);

        var employee = await empDb.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
        if (employee is null)
            return (Results.Json(new { message = "No employee profile linked. Please contact your manager." },
                statusCode: StatusCodes.Status403Forbidden), null);

        if (!employee.IsPortalEnabled)
            return (Results.Json(new { message = "Portal access not enabled. Please contact your manager." },
                statusCode: StatusCodes.Status403Forbidden), null);

        return (null, employee);
    }

    private static async Task<IResult> GetMe(HttpContext context, EmployeeDbContext empDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;

        return Results.Ok(new EmployeePortalProfileResponse(
            emp!.Id, emp.FirstName, emp.LastName, emp.Email, emp.Phone,
            emp.EmployeeNumber, emp.Department, emp.JobTitle, emp.HireDate,
            emp.IsPortalEnabled, emp.PortalCanViewSchedule,
            emp.PortalCanManageAvailability, emp.PortalCanRequestTimeOff));
    }

    private static async Task<IResult> GetSchedule(HttpContext context, EmployeeDbContext empDb, SchedulingDbContext schedDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;
        if (!emp!.PortalCanViewSchedule)
            return Results.Json(new { message = "Schedule access not enabled." }, statusCode: 403);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var until = today.AddDays(28);

        var shifts = await schedDb.ScheduleShifts
            .Include(s => s.Schedule)
            .Where(s => s.EmployeeId == emp.Id && s.Date >= today && s.Date <= until)
            .OrderBy(s => s.Date).ThenBy(s => s.ScheduledStartTime)
            .Select(s => new PortalShiftResponse(
                s.Id, s.Date, s.ScheduledStartTime, s.ScheduledEndTime,
                s.PositionId, s.Status, s.Schedule.WorkLocationId, s.Notes))
            .ToListAsync();

        return Results.Ok(shifts);
    }

    private static async Task<IResult> GetHours(HttpContext context, EmployeeDbContext empDb, SchedulingDbContext schedDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;

        var shifts = await schedDb.ScheduleShifts
            .Where(s => s.EmployeeId == emp!.Id && s.ActualStartTime != null && s.ActualEndTime != null)
            .OrderByDescending(s => s.Date)
            .Select(s => new PortalHoursResponse(
                s.Id, s.Date, s.ActualStartTime!.Value, s.ActualEndTime!.Value,
                s.PositionId, s.Status))
            .ToListAsync();

        return Results.Ok(shifts);
    }

    private static async Task<IResult> GetAvailability(HttpContext context, EmployeeDbContext empDb, SchedulingDbContext schedDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;
        if (!emp!.PortalCanManageAvailability)
            return Results.Json(new { message = "Availability management not enabled." }, statusCode: 403);

        var rules = await schedDb.EmployeeAvailabilities
            .Where(a => a.EmployeeId == emp.Id)
            .OrderBy(a => a.DayOfWeek)
            .Select(a => new PortalAvailabilityResponse(a.Id, a.DayOfWeek, a.IsAvailable, a.EarliestStartTime, a.LatestEndTime, a.Notes))
            .ToListAsync();

        return Results.Ok(rules);
    }

    private static async Task<IResult> UpsertAvailability(int dayOfWeek, PortalAvailabilityRequest request,
        HttpContext context, EmployeeDbContext empDb, SchedulingDbContext schedDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;
        if (!emp!.PortalCanManageAvailability)
            return Results.Json(new { message = "Availability management not enabled." }, statusCode: 403);
        if (dayOfWeek < 0 || dayOfWeek > 6)
            return Results.BadRequest(new { message = "DayOfWeek must be 0-6." });
        if (request.IsAvailable && request.EarliestStartTime.HasValue && request.LatestEndTime.HasValue
            && request.LatestEndTime <= request.EarliestStartTime)
            return Results.BadRequest(new { message = "LatestEndTime must be after EarliestStartTime." });

        var existing = await schedDb.EmployeeAvailabilities
            .FirstOrDefaultAsync(a => a.EmployeeId == emp.Id && a.DayOfWeek == dayOfWeek);

        if (existing is null)
        {
            existing = new EmployeeAvailability
            {
                Id = Guid.NewGuid(),
                EmployeeId = emp.Id,
                TenantId = emp.TenantId,
                DayOfWeek = dayOfWeek
            };
            schedDb.EmployeeAvailabilities.Add(existing);
        }

        existing.IsAvailable = request.IsAvailable;
        existing.EarliestStartTime = request.IsAvailable ? request.EarliestStartTime : null;
        existing.LatestEndTime = request.IsAvailable ? request.LatestEndTime : null;
        existing.Notes = request.Notes?.Trim();

        await schedDb.SaveChangesAsync();
        return Results.Ok(new PortalAvailabilityResponse(existing.Id, existing.DayOfWeek, existing.IsAvailable,
            existing.EarliestStartTime, existing.LatestEndTime, existing.Notes));
    }

    private static async Task<IResult> DeleteAvailability(int dayOfWeek,
        HttpContext context, EmployeeDbContext empDb, SchedulingDbContext schedDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;
        if (!emp!.PortalCanManageAvailability)
            return Results.Json(new { message = "Availability management not enabled." }, statusCode: 403);

        var rule = await schedDb.EmployeeAvailabilities
            .FirstOrDefaultAsync(a => a.EmployeeId == emp.Id && a.DayOfWeek == dayOfWeek);
        if (rule is null) return Results.NotFound();

        schedDb.EmployeeAvailabilities.Remove(rule);
        await schedDb.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetTimeOff(HttpContext context, EmployeeDbContext empDb, SchedulingDbContext schedDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;
        if (!emp!.PortalCanRequestTimeOff)
            return Results.Json(new { message = "Time-off requests not enabled." }, statusCode: 403);

        var requests = await schedDb.TimeOffRequests
            .Where(t => t.EmployeeId == emp.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PortalTimeOffResponse(t.Id, t.StartDate, t.EndDate, t.Type, t.Status, t.Notes, t.ReviewerNotes, t.CreatedAt))
            .ToListAsync();

        return Results.Ok(requests);
    }

    private static async Task<IResult> RequestTimeOff(PortalTimeOffRequest request,
        HttpContext context, EmployeeDbContext empDb, SchedulingDbContext schedDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;
        if (!emp!.PortalCanRequestTimeOff)
            return Results.Json(new { message = "Time-off requests not enabled." }, statusCode: 403);
        if (request.EndDate < request.StartDate)
            return Results.BadRequest(new { message = "End date must be on or after start date." });

        var t = new TimeOffRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = emp.Id,
            TenantId = emp.TenantId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Type = request.Type,
            Notes = request.Notes?.Trim(),
            Status = TimeOffStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        schedDb.TimeOffRequests.Add(t);
        await schedDb.SaveChangesAsync();
        return Results.Created($"/api/employee-portal/me/time-off/{t.Id}",
            new PortalTimeOffResponse(t.Id, t.StartDate, t.EndDate, t.Type, t.Status, t.Notes, t.ReviewerNotes, t.CreatedAt));
    }

    private static async Task<IResult> CancelTimeOff(Guid id,
        HttpContext context, EmployeeDbContext empDb, SchedulingDbContext schedDb)
    {
        var (err, emp) = await ResolveEmployee(context, empDb);
        if (err is not null) return err;
        if (!emp!.PortalCanRequestTimeOff)
            return Results.Json(new { message = "Time-off requests not enabled." }, statusCode: 403);

        var t = await schedDb.TimeOffRequests.FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == emp.Id);
        if (t is null) return Results.NotFound();
        if (t.Status != TimeOffStatus.Pending)
            return Results.BadRequest(new { message = "Only pending requests can be cancelled." });

        t.Status = TimeOffStatus.Cancelled;
        t.UpdatedAt = DateTime.UtcNow;
        await schedDb.SaveChangesAsync();
        return Results.NoContent();
    }
}

public record EmployeePortalProfileResponse(
    Guid Id, string FirstName, string LastName, string Email, string? Phone,
    string EmployeeNumber, string? Department, string? JobTitle, DateOnly HireDate,
    bool IsPortalEnabled, bool CanViewSchedule, bool CanManageAvailability, bool CanRequestTimeOff);

public record PortalShiftResponse(
    Guid Id, DateOnly Date, TimeOnly ScheduledStart, TimeOnly ScheduledEnd,
    Guid PositionId, WarpBusiness.Scheduling.Models.ShiftStatus Status, Guid WorkLocationId, string? Notes);

public record PortalHoursResponse(
    Guid Id, DateOnly Date, TimeOnly ActualStart, TimeOnly ActualEnd,
    Guid PositionId, WarpBusiness.Scheduling.Models.ShiftStatus Status);

public record PortalAvailabilityResponse(Guid Id, int DayOfWeek, bool IsAvailable,
    TimeOnly? EarliestStartTime, TimeOnly? LatestEndTime, string? Notes);

public record PortalAvailabilityRequest(bool IsAvailable, TimeOnly? EarliestStartTime, TimeOnly? LatestEndTime, string? Notes);

public record PortalTimeOffResponse(
    Guid Id, DateOnly StartDate, DateOnly EndDate,
    WarpBusiness.Scheduling.Models.TimeOffType Type,
    WarpBusiness.Scheduling.Models.TimeOffStatus Status,
    string? Notes, string? ReviewerNotes, DateTime CreatedAt);

public record PortalTimeOffRequest(DateOnly StartDate, DateOnly EndDate, WarpBusiness.Scheduling.Models.TimeOffType Type, string? Notes);
