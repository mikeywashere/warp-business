using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Endpoints;

public static class PositionEndpoints
{
    public static void MapPositionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/positions")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> GetAll(HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var positions = await db.Positions
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Name)
            .Select(p => ToResponse(p))
            .ToListAsync();

        return Results.Ok(positions);
    }

    private static async Task<IResult> GetById(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var position = await db.Positions
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        return position is null ? Results.NotFound() : Results.Ok(ToResponse(position));
    }

    private static async Task<IResult> Create(CreatePositionRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });

        var position = new Position
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#6B7280" : request.Color.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Positions.Add(position);
        await db.SaveChangesAsync();

        return Results.Created($"/api/scheduling/positions/{position.Id}", ToResponse(position));
    }

    private static async Task<IResult> Update(Guid id, UpdatePositionRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (position is null)
            return Results.NotFound();

        position.Name = request.Name.Trim();
        position.Description = request.Description?.Trim();
        position.Color = string.IsNullOrWhiteSpace(request.Color) ? "#6B7280" : request.Color.Trim();
        position.IsActive = request.IsActive;
        position.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(position));
    }

    private static async Task<IResult> Delete(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (position is null)
            return Results.NotFound();

        var inUse = await db.ScheduleShifts.AnyAsync(s => s.PositionId == id);
        if (inUse)
            return Results.BadRequest(new { message = "Cannot delete a position that has shifts assigned. Deactivate it instead." });

        db.Positions.Remove(position);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static PositionResponse ToResponse(Position p) => new(
        p.Id, p.TenantId, p.Name, p.Description, p.Color, p.IsActive, p.CreatedAt, p.UpdatedAt);
}

public record PositionResponse(Guid Id, Guid TenantId, string Name, string? Description, string Color, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public record CreatePositionRequest(string Name, string? Description, string? Color);
public record UpdatePositionRequest(string Name, string? Description, string? Color, bool IsActive);
