using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Endpoints;

public static class WorkLocationEndpoints
{
    public static void MapWorkLocationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/locations")
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

        var locations = await db.WorkLocations
            .Where(l => l.TenantId == tenantId)
            .OrderBy(l => l.Name)
            .Select(l => ToResponse(l))
            .ToListAsync();

        return Results.Ok(locations);
    }

    private static async Task<IResult> GetById(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var location = await db.WorkLocations
            .Where(l => l.Id == id && l.TenantId == tenantId)
            .FirstOrDefaultAsync();

        return location is null ? Results.NotFound() : Results.Ok(ToResponse(location));
    }

    private static async Task<IResult> Create(CreateWorkLocationRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });

        var location = new WorkLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            State = request.State.Trim().ToUpperInvariant(),
            Country = request.Country?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.WorkLocations.Add(location);
        await db.SaveChangesAsync();

        return Results.Created($"/api/scheduling/locations/{location.Id}", ToResponse(location));
    }

    private static async Task<IResult> Update(Guid id, UpdateWorkLocationRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var location = await db.WorkLocations.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);
        if (location is null)
            return Results.NotFound();

        location.Name = request.Name.Trim();
        location.Address = request.Address?.Trim();
        location.City = request.City?.Trim();
        location.State = request.State.Trim().ToUpperInvariant();
        location.Country = request.Country?.Trim();
        location.IsActive = request.IsActive;
        location.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(location));
    }

    private static async Task<IResult> Delete(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var location = await db.WorkLocations.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);
        if (location is null)
            return Results.NotFound();

        var hasSchedules = await db.Schedules.AnyAsync(s => s.WorkLocationId == id);
        if (hasSchedules)
            return Results.BadRequest(new { message = "Cannot delete a location that has schedules. Deactivate it instead." });

        db.WorkLocations.Remove(location);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static WorkLocationResponse ToResponse(WorkLocation l) => new(
        l.Id, l.TenantId, l.Name, l.Address, l.City, l.State, l.Country, l.IsActive, l.CreatedAt, l.UpdatedAt);
}

public record WorkLocationResponse(Guid Id, Guid TenantId, string Name, string? Address, string? City, string State, string? Country, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public record CreateWorkLocationRequest(string Name, string? Address, string? City, string State, string? Country);
public record UpdateWorkLocationRequest(string Name, string? Address, string? City, string State, string? Country, bool IsActive);
