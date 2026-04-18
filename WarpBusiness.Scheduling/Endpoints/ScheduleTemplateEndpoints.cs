using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Scheduling.Data;
using WarpBusiness.Scheduling.Models;

namespace WarpBusiness.Scheduling.Endpoints;

public static class ScheduleTemplateEndpoints
{
    public static void MapScheduleTemplateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scheduling/templates")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        // Staffing blocks sub-resource
        group.MapGet("/{templateId:guid}/blocks", GetBlocks);
        group.MapPost("/{templateId:guid}/blocks", AddBlock);
        group.MapPut("/{templateId:guid}/blocks/{blockId:guid}", UpdateBlock);
        group.MapDelete("/{templateId:guid}/blocks/{blockId:guid}", DeleteBlock);
    }

    private static async Task<IResult> GetAll(HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var templates = await db.ScheduleTemplates
            .Where(t => t.TenantId == tenantId)
            .Include(t => t.WorkLocation)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return Results.Ok(templates.Select(ToResponse));
    }

    private static async Task<IResult> GetById(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var template = await db.ScheduleTemplates
            .Include(t => t.WorkLocation)
            .Include(t => t.StaffingBlocks)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        return template is null ? Results.NotFound() : Results.Ok(ToDetailResponse(template));
    }

    private static async Task<IResult> Create(CreateScheduleTemplateRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });

        var locationExists = await db.WorkLocations.AnyAsync(l => l.Id == request.WorkLocationId && l.TenantId == tenantId);
        if (!locationExists)
            return Results.BadRequest(new { message = "Work location not found." });

        var template = new ScheduleTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkLocationId = request.WorkLocationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.ScheduleTemplates.Add(template);
        await db.SaveChangesAsync();

        return Results.Created($"/api/scheduling/templates/{template.Id}", ToResponse(template));
    }

    private static async Task<IResult> Update(Guid id, UpdateScheduleTemplateRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var template = await db.ScheduleTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
        if (template is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });

        template.Name = request.Name.Trim();
        template.Description = request.Description?.Trim();
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(ToResponse(template));
    }

    private static async Task<IResult> Delete(Guid id, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var template = await db.ScheduleTemplates
            .Include(t => t.StaffingBlocks)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (template is null)
            return Results.NotFound();

        db.ScheduleTemplates.Remove(template);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetBlocks(Guid templateId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var exists = await db.ScheduleTemplates.AnyAsync(t => t.Id == templateId && t.TenantId == tenantId);
        if (!exists)
            return Results.NotFound();

        var blocks = await db.StaffingBlocks
            .Where(b => b.TemplateId == templateId)
            .OrderBy(b => b.DayOfWeek).ThenBy(b => b.StartTime)
            .ToListAsync();

        return Results.Ok(blocks.Select(ToBlockResponse));
    }

    private static async Task<IResult> AddBlock(Guid templateId, CreateStaffingBlockRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var exists = await db.ScheduleTemplates.AnyAsync(t => t.Id == templateId && t.TenantId == tenantId);
        if (!exists)
            return Results.NotFound();

        if (request.DayOfWeek < 0 || request.DayOfWeek > 6)
            return Results.BadRequest(new { message = "DayOfWeek must be 0–6 (0=Sunday)." });

        if (request.RequiredCount < 1)
            return Results.BadRequest(new { message = "RequiredCount must be at least 1." });

        var block = new StaffingBlock
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            PositionId = request.PositionId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            RequiredCount = request.RequiredCount
        };

        db.StaffingBlocks.Add(block);
        await db.SaveChangesAsync();

        return Results.Created($"/api/scheduling/templates/{templateId}/blocks/{block.Id}", ToBlockResponse(block));
    }

    private static async Task<IResult> UpdateBlock(Guid templateId, Guid blockId, UpdateStaffingBlockRequest request, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var exists = await db.ScheduleTemplates.AnyAsync(t => t.Id == templateId && t.TenantId == tenantId);
        if (!exists)
            return Results.NotFound();

        var block = await db.StaffingBlocks.FirstOrDefaultAsync(b => b.Id == blockId && b.TemplateId == templateId);
        if (block is null)
            return Results.NotFound();

        block.PositionId = request.PositionId;
        block.DayOfWeek = request.DayOfWeek;
        block.StartTime = request.StartTime;
        block.EndTime = request.EndTime;
        block.RequiredCount = request.RequiredCount;

        await db.SaveChangesAsync();
        return Results.Ok(ToBlockResponse(block));
    }

    private static async Task<IResult> DeleteBlock(Guid templateId, Guid blockId, HttpContext context, SchedulingDbContext db)
    {
        if (context.Items["TenantId"] is not Guid tenantId)
            return Results.BadRequest(new { message = "Tenant context required." });

        var exists = await db.ScheduleTemplates.AnyAsync(t => t.Id == templateId && t.TenantId == tenantId);
        if (!exists)
            return Results.NotFound();

        var block = await db.StaffingBlocks.FirstOrDefaultAsync(b => b.Id == blockId && b.TemplateId == templateId);
        if (block is null)
            return Results.NotFound();

        db.StaffingBlocks.Remove(block);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static ScheduleTemplateResponse ToResponse(ScheduleTemplate t) => new(
        t.Id, t.TenantId, t.WorkLocationId, t.WorkLocation?.Name, t.Name, t.Description, t.IsActive, t.CreatedAt, t.UpdatedAt, null);

    private static ScheduleTemplateResponse ToDetailResponse(ScheduleTemplate t) => new(
        t.Id, t.TenantId, t.WorkLocationId, t.WorkLocation?.Name, t.Name, t.Description, t.IsActive, t.CreatedAt, t.UpdatedAt,
        t.StaffingBlocks.Select(ToBlockResponse).ToList());

    private static StaffingBlockResponse ToBlockResponse(StaffingBlock b) => new(
        b.Id, b.TemplateId, b.PositionId, b.DayOfWeek, b.StartTime, b.EndTime, b.RequiredCount);
}

public record ScheduleTemplateResponse(Guid Id, Guid TenantId, Guid WorkLocationId, string? WorkLocationName, string Name, string? Description, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<StaffingBlockResponse>? Blocks);
public record StaffingBlockResponse(Guid Id, Guid TemplateId, Guid PositionId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int RequiredCount);
public record CreateScheduleTemplateRequest(string Name, string? Description, Guid WorkLocationId);
public record UpdateScheduleTemplateRequest(string Name, string? Description, bool IsActive);
public record CreateStaffingBlockRequest(Guid PositionId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int RequiredCount);
public record UpdateStaffingBlockRequest(Guid PositionId, int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int RequiredCount);
