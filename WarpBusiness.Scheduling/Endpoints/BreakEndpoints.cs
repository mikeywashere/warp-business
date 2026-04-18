using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Endpoints;

public static class BreakEndpoints
{
    public static void MapBreakEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/schedules/{scheduleId:guid}/shifts/{shiftId:guid}/breaks")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapPut("/{breakId:guid}", Update);
        group.MapDelete("/{breakId:guid}", Delete);
    }

    private static async Task<IResult> GetAll(Guid scheduleId, Guid shiftId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (!await IsShiftAccessible(scheduleId, shiftId, tenantId, db))
            return Results.NotFound();

        var breaks = await db.ScheduleBreaks
            .Where(b => b.ShiftId == shiftId)
            .OrderBy(b => b.ScheduledStartTime)
            .ToListAsync();

        return Results.Ok(breaks.Select(ToResponse));
    }

    private static async Task<IResult> Create(Guid scheduleId, Guid shiftId, CreateBreakRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (!await IsShiftAccessible(scheduleId, shiftId, tenantId, db))
            return Results.NotFound();

        var breakItem = new ScheduleBreak
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            BreakType = request.BreakType,
            IsPaid = request.IsPaid,
            ScheduledStartTime = request.ScheduledStartTime,
            ScheduledEndTime = request.ScheduledEndTime,
            WasTaken = false
        };

        db.ScheduleBreaks.Add(breakItem);
        await db.SaveChangesAsync();

        return Results.Created(
            $"/api/scheduling/schedules/{scheduleId}/shifts/{shiftId}/breaks/{breakItem.Id}",
            ToResponse(breakItem));
    }

    private static async Task<IResult> Update(Guid scheduleId, Guid shiftId, Guid breakId, UpdateBreakRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (!await IsShiftAccessible(scheduleId, shiftId, tenantId, db))
            return Results.NotFound();

        var breakItem = await db.ScheduleBreaks.FirstOrDefaultAsync(b => b.Id == breakId && b.ShiftId == shiftId);
        if (breakItem is null)
            return Results.NotFound();

        breakItem.BreakType = request.BreakType;
        breakItem.IsPaid = request.IsPaid;
        breakItem.ScheduledStartTime = request.ScheduledStartTime;
        breakItem.ScheduledEndTime = request.ScheduledEndTime;
        breakItem.ActualStartTime = request.ActualStartTime;
        breakItem.ActualEndTime = request.ActualEndTime;
        breakItem.WasTaken = request.WasTaken;

        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(breakItem));
    }

    private static async Task<IResult> Delete(Guid scheduleId, Guid shiftId, Guid breakId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (!await IsShiftAccessible(scheduleId, shiftId, tenantId, db))
            return Results.NotFound();

        var breakItem = await db.ScheduleBreaks.FirstOrDefaultAsync(b => b.Id == breakId && b.ShiftId == shiftId);
        if (breakItem is null)
            return Results.NotFound();

        db.ScheduleBreaks.Remove(breakItem);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<bool> IsShiftAccessible(Guid scheduleId, Guid shiftId, Guid tenantId, SchedulingDbContext db)
    {
        var scheduleExists = await db.Schedules.AnyAsync(s => s.Id == scheduleId && s.TenantId == tenantId);
        if (!scheduleExists)
            return false;

        return await db.ScheduleShifts.AnyAsync(s => s.Id == shiftId && s.ScheduleId == scheduleId);
    }

    private static BreakResponse ToResponse(ScheduleBreak b) => new(
        b.Id, b.ShiftId, b.BreakType, b.IsPaid,
        b.ScheduledStartTime, b.ScheduledEndTime,
        b.ActualStartTime, b.ActualEndTime,
        b.WasTaken);
}

public record BreakResponse(Guid Id, Guid ShiftId, BreakType BreakType, bool IsPaid,
    TimeOnly? ScheduledStartTime, TimeOnly? ScheduledEndTime,
    TimeOnly? ActualStartTime, TimeOnly? ActualEndTime,
    bool WasTaken);

public record CreateBreakRequest(BreakType BreakType, bool IsPaid, TimeOnly? ScheduledStartTime, TimeOnly? ScheduledEndTime);
public record UpdateBreakRequest(BreakType BreakType, bool IsPaid, TimeOnly? ScheduledStartTime, TimeOnly? ScheduledEndTime, TimeOnly? ActualStartTime, TimeOnly? ActualEndTime, bool WasTaken);
