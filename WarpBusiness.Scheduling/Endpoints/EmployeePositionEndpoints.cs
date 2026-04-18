using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Endpoints;

public static class EmployeePositionEndpoints
{
    public static void MapEmployeePositionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/employees/{employeeId:guid}/positions", GetEmployeePositions);
        group.MapPost("/employees/{employeeId:guid}/positions/{positionId:guid}", AssignPosition);
        group.MapDelete("/employees/{employeeId:guid}/positions/{positionId:guid}", RemovePosition);
        group.MapGet("/positions/{positionId:guid}/employees", GetPositionEmployees);
    }

    private static async Task<IResult> GetEmployeePositions(Guid employeeId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var positions = await db.EmployeePositions
            .Where(ep => ep.EmployeeId == employeeId && ep.TenantId == tenantId)
            .Include(ep => ep.Position)
            .OrderBy(ep => ep.Position.Name)
            .Select(ep => new EmployeePositionResponse(
                ep.EmployeeId,
                ep.PositionId,
                ep.Position.Name,
                ep.Position.Color,
                ep.Position.IsActive,
                ep.AssignedAt))
            .ToListAsync();

        return Results.Ok(positions);
    }

    private static async Task<IResult> AssignPosition(Guid employeeId, Guid positionId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == positionId && p.TenantId == tenantId);
        if (position is null)
            return Results.NotFound(new { message = "Position not found." });

        if (!position.IsActive)
            return Results.BadRequest(new { message = "Cannot assign an inactive position." });

        var existing = await db.EmployeePositions
            .FirstOrDefaultAsync(ep => ep.EmployeeId == employeeId && ep.PositionId == positionId);
        if (existing is not null)
            return Results.Conflict(new { message = "Position already assigned to this employee." });

        db.EmployeePositions.Add(new EmployeePosition
        {
            EmployeeId = employeeId,
            PositionId = positionId,
            TenantId = tenantId,
            AssignedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return Results.Created($"/api/scheduling/employees/{employeeId}/positions/{positionId}", null);
    }

    private static async Task<IResult> RemovePosition(Guid employeeId, Guid positionId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var ep = await db.EmployeePositions
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.PositionId == positionId && e.TenantId == tenantId);
        if (ep is null)
            return Results.NotFound();

        db.EmployeePositions.Remove(ep);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetPositionEmployees(Guid positionId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var employeeIds = await db.EmployeePositions
            .Where(ep => ep.PositionId == positionId && ep.TenantId == tenantId)
            .Select(ep => ep.EmployeeId)
            .ToListAsync();

        return Results.Ok(employeeIds);
    }
}

public record EmployeePositionResponse(Guid EmployeeId, Guid PositionId, string PositionName, string Color, bool IsActive, DateTime AssignedAt);
