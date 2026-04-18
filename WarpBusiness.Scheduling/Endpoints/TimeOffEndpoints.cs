using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Endpoints;

public static class TimeOffEndpoints
{
    public static void MapTimeOffEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/time-off")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/{id:guid}/approve", Approve);
        group.MapPost("/{id:guid}/deny", Deny);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> GetAll(HttpContext context, SchedulingDbContext db,
        Guid? employeeId = null, string? status = null)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var query = db.TimeOffRequests.Where(t => t.TenantId == tenantId);
        if (employeeId.HasValue)
            query = query.Where(t => t.EmployeeId == employeeId.Value);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TimeOffStatus>(status, true, out var s))
            query = query.Where(t => t.Status == s);

        var results = await query.OrderByDescending(t => t.CreatedAt).Select(t => ToResponse(t)).ToListAsync();
        return Results.Ok(results);
    }

    private static async Task<IResult> GetById(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var t = await db.TimeOffRequests.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
        return t is null ? Results.NotFound() : Results.Ok(ToResponse(t));
    }

    private static async Task<IResult> Approve(Guid id, ReviewTimeOffRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var t = await db.TimeOffRequests.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
        if (t is null) return Results.NotFound();
        if (t.Status != TimeOffStatus.Pending)
            return Results.BadRequest(new { message = "Only pending requests can be approved." });

        var userId = context.User.FindFirst("sub")?.Value;
        t.Status = TimeOffStatus.Approved;
        t.ReviewerNotes = request.ReviewerNotes?.Trim();
        t.ReviewedByUserId = Guid.TryParse(userId, out var uid) ? uid : null;
        t.ReviewedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(t));
    }

    private static async Task<IResult> Deny(Guid id, ReviewTimeOffRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var t = await db.TimeOffRequests.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
        if (t is null) return Results.NotFound();
        if (t.Status != TimeOffStatus.Pending)
            return Results.BadRequest(new { message = "Only pending requests can be denied." });

        var userId = context.User.FindFirst("sub")?.Value;
        t.Status = TimeOffStatus.Denied;
        t.ReviewerNotes = request.ReviewerNotes?.Trim();
        t.ReviewedByUserId = Guid.TryParse(userId, out var uid) ? uid : null;
        t.ReviewedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(t));
    }

    private static async Task<IResult> Delete(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var t = await db.TimeOffRequests.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
        if (t is null) return Results.NotFound();
        db.TimeOffRequests.Remove(t);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static TimeOffResponse ToResponse(TimeOffRequest t) =>
        new(t.Id, t.EmployeeId, t.TenantId, t.StartDate, t.EndDate, t.Type, t.Status,
            t.Notes, t.ReviewerNotes, t.ReviewedByUserId, t.ReviewedAt, t.CreatedAt, t.UpdatedAt);
}

public record ReviewTimeOffRequest(string? ReviewerNotes);
public record TimeOffResponse(Guid Id, Guid EmployeeId, Guid TenantId,
    DateOnly StartDate, DateOnly EndDate,
    TimeOffType Type, TimeOffStatus Status,
    string? Notes, string? ReviewerNotes,
    Guid? ReviewedByUserId, DateTime? ReviewedAt,
    DateTime CreatedAt, DateTime UpdatedAt);
