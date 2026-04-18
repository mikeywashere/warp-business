using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Endpoints;

public static class EmployeeAvailabilityEndpoints
{
    public static void MapEmployeeAvailabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/employees")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/{employeeId:guid}/availability", GetAvailability);
        group.MapPut("/{employeeId:guid}/availability/{dayOfWeek:int}", UpsertAvailability);
        group.MapDelete("/{employeeId:guid}/availability/{dayOfWeek:int}", DeleteAvailability);
    }

    private static async Task<IResult> GetAvailability(Guid employeeId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var rules = await db.EmployeeAvailabilities
            .Where(a => a.EmployeeId == employeeId && a.TenantId == tenantId)
            .OrderBy(a => a.DayOfWeek)
            .Select(a => ToResponse(a))
            .ToListAsync();

        return Results.Ok(rules);
    }

    private static async Task<IResult> UpsertAvailability(
        Guid employeeId, int dayOfWeek, EmployeeAvailabilityRequest request,
        HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (dayOfWeek < 0 || dayOfWeek > 6)
            return Results.BadRequest(new { message = "DayOfWeek must be 0 (Sunday) through 6 (Saturday)." });

        if (request.IsAvailable && request.EarliestStartTime.HasValue && request.LatestEndTime.HasValue
            && request.LatestEndTime <= request.EarliestStartTime)
            return Results.BadRequest(new { message = "LatestEndTime must be after EarliestStartTime." });

        var existing = await db.EmployeeAvailabilities
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.DayOfWeek == dayOfWeek && a.TenantId == tenantId);

        if (existing is null)
        {
            existing = new EmployeeAvailability
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                TenantId = tenantId,
                DayOfWeek = dayOfWeek
            };
            db.EmployeeAvailabilities.Add(existing);
        }

        existing.IsAvailable = request.IsAvailable;
        existing.EarliestStartTime = request.IsAvailable ? request.EarliestStartTime : null;
        existing.LatestEndTime = request.IsAvailable ? request.LatestEndTime : null;
        existing.Notes = request.Notes?.Trim();

        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(existing));
    }

    private static async Task<IResult> DeleteAvailability(
        Guid employeeId, int dayOfWeek, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (dayOfWeek < 0 || dayOfWeek > 6)
            return Results.BadRequest(new { message = "DayOfWeek must be 0 (Sunday) through 6 (Saturday)." });

        var rule = await db.EmployeeAvailabilities
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.DayOfWeek == dayOfWeek && a.TenantId == tenantId);
        if (rule is null)
            return Results.NotFound();

        db.EmployeeAvailabilities.Remove(rule);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static EmployeeAvailabilityResponse ToResponse(EmployeeAvailability a) =>
        new(a.Id, a.EmployeeId, a.TenantId, a.DayOfWeek, a.IsAvailable, a.EarliestStartTime, a.LatestEndTime, a.Notes);
}

public record EmployeeAvailabilityRequest(bool IsAvailable, TimeOnly? EarliestStartTime, TimeOnly? LatestEndTime, string? Notes);
public record EmployeeAvailabilityResponse(Guid Id, Guid EmployeeId, Guid TenantId, int DayOfWeek, bool IsAvailable, TimeOnly? EarliestStartTime, TimeOnly? LatestEndTime, string? Notes);
