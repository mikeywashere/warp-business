using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;
using WarpBusiness.Scheduling.Services;

namespace WarpBusiness.Scheduling.Endpoints;

public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/schedules")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapPatch("/{id:guid}/status", UpdateStatus);
        group.MapDelete("/{id:guid}", Delete);

        // Shifts sub-resource
        group.MapGet("/{scheduleId:guid}/shifts", GetShifts);
        group.MapGet("/{scheduleId:guid}/shifts/{shiftId:guid}", GetShift);
        group.MapPost("/{scheduleId:guid}/shifts", CreateShift);
        group.MapPut("/{scheduleId:guid}/shifts/{shiftId:guid}", UpdateShift);
        group.MapDelete("/{scheduleId:guid}/shifts/{shiftId:guid}", DeleteShift);

        // Break validation
        group.MapGet("/{scheduleId:guid}/shifts/{shiftId:guid}/validate-breaks", ValidateBreaks);
    }

    private static async Task<IResult> GetAll(HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var schedules = await db.Schedules
            .Where(s => s.TenantId == tenantId)
            .Include(s => s.WorkLocation)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();

        return Results.Ok(schedules.Select(ToResponse));
    }

    private static async Task<IResult> GetById(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var schedule = await db.Schedules
            .Include(s => s.WorkLocation)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

        return schedule is null ? Results.NotFound() : Results.Ok(ToResponse(schedule));
    }

    private static async Task<IResult> Create(CreateScheduleRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });

        if (request.EndDate < request.StartDate)
            return Results.BadRequest(new { message = "EndDate must be on or after StartDate." });

        var locationExists = await db.WorkLocations.AnyAsync(l => l.Id == request.WorkLocationId && l.TenantId == tenantId);
        if (!locationExists)
            return Results.BadRequest(new { message = "Work location not found." });

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkLocationId = request.WorkLocationId,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = ScheduleStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        return Results.Created($"/api/scheduling/schedules/{schedule.Id}", ToResponse(schedule));
    }

    private static async Task<IResult> Update(Guid id, UpdateScheduleRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (schedule is null)
            return Results.NotFound();

        if (schedule.Status is ScheduleStatus.Completed or ScheduleStatus.Archived)
            return Results.BadRequest(new { message = "Cannot edit a completed or archived schedule." });

        schedule.Name = request.Name.Trim();
        schedule.StartDate = request.StartDate;
        schedule.EndDate = request.EndDate;
        schedule.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(schedule));
    }

    private static async Task<IResult> UpdateStatus(Guid id, UpdateScheduleStatusRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (schedule is null)
            return Results.NotFound();

        schedule.Status = request.Status;
        schedule.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(schedule));
    }

    private static async Task<IResult> Delete(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
        if (schedule is null)
            return Results.NotFound();

        if (schedule.Status is not (ScheduleStatus.Draft or ScheduleStatus.Archived))
            return Results.BadRequest(new { message = "Only draft or archived schedules can be deleted." });

        db.Schedules.Remove(schedule);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // --- Shifts ---

    private static async Task<IResult> GetShifts(Guid scheduleId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var scheduleExists = await db.Schedules.AnyAsync(s => s.Id == scheduleId && s.TenantId == tenantId);
        if (!scheduleExists)
            return Results.NotFound();

        var shifts = await db.ScheduleShifts
            .Where(s => s.ScheduleId == scheduleId)
            .Include(s => s.Breaks)
            .OrderBy(s => s.Date).ThenBy(s => s.ScheduledStartTime)
            .ToListAsync();

        return Results.Ok(shifts.Select(ToShiftResponse));
    }

    private static async Task<IResult> GetShift(Guid scheduleId, Guid shiftId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var scheduleExists = await db.Schedules.AnyAsync(s => s.Id == scheduleId && s.TenantId == tenantId);
        if (!scheduleExists)
            return Results.NotFound();

        var shift = await db.ScheduleShifts
            .Include(s => s.Breaks)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.ScheduleId == scheduleId);

        return shift is null ? Results.NotFound() : Results.Ok(ToShiftResponse(shift));
    }

    private static async Task<IResult> CreateShift(Guid scheduleId, CreateShiftRequest request, HttpContext context,
        SchedulingDbContext db, BreakCalculationService breakCalc)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var schedule = await db.Schedules
            .Include(s => s.WorkLocation)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.TenantId == tenantId);

        if (schedule is null)
            return Results.NotFound();

        if (request.Date < schedule.StartDate || request.Date > schedule.EndDate)
            return Results.BadRequest(new { message = "Shift date must be within the schedule date range." });

        var shift = new ScheduleShift
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            EmployeeId = request.EmployeeId,
            PositionId = request.PositionId,
            Date = request.Date,
            ScheduledStartTime = request.ScheduledStartTime,
            ScheduledEndTime = request.ScheduledEndTime,
            Status = ShiftStatus.Scheduled,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.ScheduleShifts.Add(shift);

        // Auto-generate required breaks
        var requiredBreaks = await breakCalc.CalculateRequiredBreaksAsync(
            request.ScheduledStartTime,
            request.ScheduledEndTime,
            schedule.WorkLocation.State);

        foreach (var rb in requiredBreaks)
        {
            db.ScheduleBreaks.Add(new ScheduleBreak
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                BreakType = rb.Type == BreakRuleType.Rest ? BreakType.Rest : BreakType.Meal,
                IsPaid = rb.IsPaid,
                ScheduledEndTime = rb.LatestStartTime.HasValue
                    ? rb.LatestStartTime.Value.AddMinutes(rb.DurationMinutes)
                    : null,
                ScheduledStartTime = rb.LatestStartTime
            });
        }

        await db.SaveChangesAsync();

        await db.Entry(shift).Collection(s => s.Breaks).LoadAsync();
        return Results.Created($"/api/scheduling/schedules/{scheduleId}/shifts/{shift.Id}", ToShiftResponse(shift));
    }

    private static async Task<IResult> UpdateShift(Guid scheduleId, Guid shiftId, UpdateShiftRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var scheduleExists = await db.Schedules.AnyAsync(s => s.Id == scheduleId && s.TenantId == tenantId);
        if (!scheduleExists)
            return Results.NotFound();

        var shift = await db.ScheduleShifts.Include(s => s.Breaks)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.ScheduleId == scheduleId);

        if (shift is null)
            return Results.NotFound();

        shift.EmployeeId = request.EmployeeId;
        shift.PositionId = request.PositionId;
        shift.ScheduledStartTime = request.ScheduledStartTime;
        shift.ScheduledEndTime = request.ScheduledEndTime;
        shift.ActualStartTime = request.ActualStartTime;
        shift.ActualEndTime = request.ActualEndTime;
        shift.Status = request.Status;
        shift.Notes = request.Notes?.Trim();
        shift.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(ToShiftResponse(shift));
    }

    private static async Task<IResult> DeleteShift(Guid scheduleId, Guid shiftId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var scheduleExists = await db.Schedules.AnyAsync(s => s.Id == scheduleId && s.TenantId == tenantId);
        if (!scheduleExists)
            return Results.NotFound();

        var shift = await db.ScheduleShifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.ScheduleId == scheduleId);
        if (shift is null)
            return Results.NotFound();

        db.ScheduleShifts.Remove(shift);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ValidateBreaks(Guid scheduleId, Guid shiftId, HttpContext context,
        SchedulingDbContext db, BreakValidationService breakValidator)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var schedule = await db.Schedules
            .Include(s => s.WorkLocation)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.TenantId == tenantId);

        if (schedule is null)
            return Results.NotFound();

        var shiftExists = await db.ScheduleShifts.AnyAsync(s => s.Id == shiftId && s.ScheduleId == scheduleId);
        if (!shiftExists)
            return Results.NotFound();

        var violations = await breakValidator.ValidateAsync(shiftId, schedule.WorkLocation.State);
        return Results.Ok(violations);
    }

    private static ScheduleResponse ToResponse(Schedule s) => new(
        s.Id, s.TenantId, s.WorkLocationId, s.WorkLocation?.Name, s.Name,
        s.StartDate, s.EndDate, s.Status, s.CreatedAt, s.UpdatedAt);

    private static ShiftResponse ToShiftResponse(ScheduleShift s) => new(
        s.Id, s.ScheduleId, s.EmployeeId, s.PositionId, s.Date,
        s.ScheduledStartTime, s.ScheduledEndTime,
        s.ActualStartTime, s.ActualEndTime,
        s.Status, s.Notes, s.CreatedAt, s.UpdatedAt,
        s.Breaks.Select(b => new BreakResponse(b.Id, b.ShiftId, b.BreakType, b.IsPaid,
            b.ScheduledStartTime, b.ScheduledEndTime, b.ActualStartTime, b.ActualEndTime, b.WasTaken)).ToList());
}

public record ScheduleResponse(Guid Id, Guid TenantId, Guid WorkLocationId, string? WorkLocationName, string Name, DateOnly StartDate, DateOnly EndDate, ScheduleStatus Status, DateTime CreatedAt, DateTime UpdatedAt);
public record CreateScheduleRequest(string Name, Guid WorkLocationId, DateOnly StartDate, DateOnly EndDate);
public record UpdateScheduleRequest(string Name, DateOnly StartDate, DateOnly EndDate);
public record UpdateScheduleStatusRequest(ScheduleStatus Status);

public record ShiftResponse(Guid Id, Guid ScheduleId, Guid EmployeeId, Guid PositionId, DateOnly Date,
    TimeOnly ScheduledStartTime, TimeOnly ScheduledEndTime,
    TimeOnly? ActualStartTime, TimeOnly? ActualEndTime,
    ShiftStatus Status, string? Notes, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<BreakResponse> Breaks);

public record CreateShiftRequest(Guid EmployeeId, Guid PositionId, DateOnly Date, TimeOnly ScheduledStartTime, TimeOnly ScheduledEndTime, string? Notes);
public record UpdateShiftRequest(Guid EmployeeId, Guid PositionId, TimeOnly ScheduledStartTime, TimeOnly ScheduledEndTime, TimeOnly? ActualStartTime, TimeOnly? ActualEndTime, ShiftStatus Status, string? Notes);
