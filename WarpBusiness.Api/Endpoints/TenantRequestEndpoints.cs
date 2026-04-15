using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Models;

namespace WarpBusiness.Api.Endpoints;

public static class TenantRequestEndpoints
{
    public static void MapTenantRequestEndpoints(this WebApplication app)
    {
        var tenantRequests = app.MapGroup("/api/tenants/{tenantId:guid}/requests")
            .RequireAuthorization();

        tenantRequests.MapGet("/", GetTenantRequests)
            .WithName("GetTenantRequests");

        tenantRequests.MapPost("/", CreateTenantRequest)
            .WithName("CreateTenantRequest");

        tenantRequests.MapGet("/{id:guid}", GetTenantRequestById)
            .WithName("GetTenantRequestById");

        tenantRequests.MapPut("/{id:guid}/cancel", CancelTenantRequest)
            .WithName("CancelTenantRequest");

        var adminRequests = app.MapGroup("/api/admin/requests")
            .RequireAuthorization("SystemAdministrator");

        adminRequests.MapGet("/", GetAllRequests)
            .WithName("GetAllRequests");

        adminRequests.MapPut("/{id:guid}", UpdateRequest)
            .WithName("UpdateRequest");
    }

    private static async Task<IResult> GetTenantRequests(
        Guid tenantId,
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] Guid? assignedTo,
        CancellationToken cancellationToken)
    {
        if (!await AuthorizeTenantAccess(tenantId, principal, db, cancellationToken))
            return Results.Forbid();

        var query = db.TenantRequests.Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r =>
                r.Title.Contains(search) ||
                r.Description.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TenantRequestStatus>(status, true, out var statusEnum))
        {
            query = query.Where(r => r.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<TenantRequestType>(type, true, out var typeEnum))
        {
            query = query.Where(r => r.Type == typeEnum);
        }

        if (assignedTo.HasValue)
        {
            query = query.Where(r => r.AssignedToUserId == assignedTo.Value);
        }

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToResponse(r))
            .ToListAsync(cancellationToken);

        return Results.Ok(requests);
    }

    private static async Task<IResult> CreateTenantRequest(
        Guid tenantId,
        [FromBody] CreateTenantRequestRequest request,
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await AuthorizeTenantAccess(tenantId, principal, db, cancellationToken))
            return Results.Forbid();

        if (!Enum.TryParse<TenantRequestType>(request.Type, true, out var typeEnum))
            return Results.BadRequest(new { message = "Invalid request type." });

        var tenantRequest = new TenantRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title,
            Description = request.Description,
            Type = typeEnum,
            Status = TenantRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.TenantRequests.Add(tenantRequest);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/tenants/{tenantId}/requests/{tenantRequest.Id}", ToResponse(tenantRequest));
    }

    private static async Task<IResult> GetTenantRequestById(
        Guid tenantId,
        Guid id,
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await AuthorizeTenantAccess(tenantId, principal, db, cancellationToken))
            return Results.Forbid();

        var request = await db.TenantRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

        if (request is null)
            return Results.NotFound();

        return Results.Ok(ToResponse(request));
    }

    private static async Task<IResult> CancelTenantRequest(
        Guid tenantId,
        Guid id,
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await AuthorizeTenantAccess(tenantId, principal, db, cancellationToken))
            return Results.Forbid();

        var request = await db.TenantRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

        if (request is null)
            return Results.NotFound();

        if (request.Status != TenantRequestStatus.Open && request.Status != TenantRequestStatus.Pending)
            return Results.BadRequest(new { message = "Only open or pending requests can be cancelled." });

        request.Status = TenantRequestStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;
        request.ClosedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(request));
    }

    private static async Task<IResult> GetAllRequests(
        WarpBusinessDbContext db,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] Guid? assignedTo,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var query = db.TenantRequests.AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r =>
                r.Title.Contains(search) ||
                r.Description.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TenantRequestStatus>(status, true, out var statusEnum))
        {
            query = query.Where(r => r.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<TenantRequestType>(type, true, out var typeEnum))
        {
            query = query.Where(r => r.Type == typeEnum);
        }

        if (assignedTo.HasValue)
        {
            query = query.Where(r => r.AssignedToUserId == assignedTo.Value);
        }

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToResponse(r))
            .ToListAsync(cancellationToken);

        return Results.Ok(requests);
    }

    private static async Task<IResult> UpdateRequest(
        Guid id,
        [FromBody] UpdateTenantRequestRequest request,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantRequest = await db.TenantRequests.FindAsync([id], cancellationToken);
        if (tenantRequest is null)
            return Results.NotFound();

        if (!Enum.TryParse<TenantRequestStatus>(request.Status, true, out var statusEnum))
            return Results.BadRequest(new { message = "Invalid status." });

        if (!Enum.TryParse<TenantRequestType>(request.Type, true, out var typeEnum))
            return Results.BadRequest(new { message = "Invalid request type." });

        tenantRequest.Title = request.Title;
        tenantRequest.Description = request.Description;
        tenantRequest.Status = statusEnum;
        tenantRequest.Type = typeEnum;
        tenantRequest.AssignedToName = request.AssignedToName;
        tenantRequest.AssignedToUserId = request.AssignedToUserId;
        tenantRequest.Resolution = request.Resolution;
        tenantRequest.UpdatedAt = DateTime.UtcNow;

        if (statusEnum is TenantRequestStatus.Resolved or TenantRequestStatus.Closed or TenantRequestStatus.Cancelled)
        {
            tenantRequest.ClosedAt ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(tenantRequest));
    }

    private static async Task<bool> AuthorizeTenantAccess(
        Guid tenantId,
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        if (IsSystemAdministrator(principal))
            return true;

        var userId = await GetCurrentUserId(principal, db, cancellationToken);
        if (userId is null)
            return false;

        return await db.UserTenantMemberships
            .AnyAsync(m => m.UserId == userId.Value && m.TenantId == tenantId, cancellationToken);
    }

    private static bool IsSystemAdministrator(ClaimsPrincipal principal)
    {
        if (principal.IsInRole("SystemAdministrator"))
            return true;
        if (principal.HasClaim("roles", "SystemAdministrator"))
            return true;
        return principal.HasClaim("app_role", "SystemAdministrator");
    }

    private static async Task<Guid?> GetCurrentUserId(
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var subjectId = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);

        ApplicationUser? user = null;

        if (!string.IsNullOrEmpty(subjectId))
            user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakSubjectId == subjectId, cancellationToken);

        if (user is null && !string.IsNullOrEmpty(email))
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        return user?.Id;
    }

    private static TenantRequestResponse ToResponse(TenantRequest request) =>
        new(
            request.Id,
            request.TenantId,
            request.Title,
            request.Description,
            request.Status.ToString(),
            request.Type.ToString(),
            request.AssignedToName,
            request.AssignedToUserId,
            request.Resolution,
            request.CreatedAt,
            request.UpdatedAt,
            request.ClosedAt
        );
}
