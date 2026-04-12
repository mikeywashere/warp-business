using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Models;

namespace WarpBusiness.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var tenants = app.MapGroup("/api/tenants")
            .RequireAuthorization();

        tenants.MapGet("/", GetAllTenants)
            .WithName("GetAllTenants");

        tenants.MapGet("/{id:guid}", GetTenantById)
            .WithName("GetTenantById");

        tenants.MapPost("/", CreateTenant)
            .WithName("CreateTenant")
            .RequireAuthorization("SystemAdministrator");

        tenants.MapPut("/{id:guid}", UpdateTenant)
            .WithName("UpdateTenant")
            .RequireAuthorization("SystemAdministrator");

        tenants.MapDelete("/{id:guid}", DeleteTenant)
            .WithName("DeleteTenant")
            .RequireAuthorization("SystemAdministrator");

        tenants.MapGet("/{id:guid}/members", GetTenantMembers)
            .WithName("GetTenantMembers");

        tenants.MapPost("/{id:guid}/members", AddTenantMember)
            .WithName("AddTenantMember")
            .RequireAuthorization("SystemAdministrator");

        tenants.MapDelete("/{id:guid}/members/{userId:guid}", RemoveTenantMember)
            .WithName("RemoveTenantMember")
            .RequireAuthorization("SystemAdministrator");

        // Tenant selection endpoints under /api/users/me
        var me = app.MapGroup("/api/users/me")
            .RequireAuthorization();

        me.MapGet("/tenants", GetMyTenants)
            .WithName("GetMyTenants");

        me.MapPost("/tenant", SetActiveTenant)
            .WithName("SetActiveTenant");
    }

    private static async Task<IResult> GetAllTenants(
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var isAdmin = IsSystemAdministrator(principal);

        if (isAdmin)
        {
            var allTenants = await db.Tenants
                .OrderBy(t => t.Name)
                .Select(t => ToResponse(t))
                .ToListAsync(cancellationToken);
            return Results.Ok(allTenants);
        }

        // Regular users only see tenants they belong to
        var userId = await GetCurrentUserId(principal, db, cancellationToken);
        if (userId is null)
            return Results.NotFound(new { message = "User profile not found." });

        var userTenants = await db.UserTenantMemberships
            .Where(m => m.UserId == userId.Value)
            .Select(m => ToResponse(m.Tenant))
            .ToListAsync(cancellationToken);

        return Results.Ok(userTenants);
    }

    private static async Task<IResult> GetTenantById(
        Guid id,
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.FindAsync([id], cancellationToken);
        if (tenant is null)
            return Results.NotFound();

        if (!IsSystemAdministrator(principal))
        {
            var userId = await GetCurrentUserId(principal, db, cancellationToken);
            if (userId is null)
                return Results.NotFound(new { message = "User profile not found." });

            var isMember = await db.UserTenantMemberships
                .AnyAsync(m => m.UserId == userId.Value && m.TenantId == id, cancellationToken);
            if (!isMember)
                return Results.Forbid();
        }

        return Results.Ok(ToResponse(tenant));
    }

    private static async Task<IResult> CreateTenant(
        [FromBody] CreateTenantRequest request,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        if (await db.Tenants.AnyAsync(t => t.Slug == request.Slug, cancellationToken))
            return Results.Conflict(new { message = "A tenant with this slug already exists." });

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/tenants/{tenant.Id}", ToResponse(tenant));
    }

    private static async Task<IResult> UpdateTenant(
        Guid id,
        [FromBody] UpdateTenantRequest request,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.FindAsync([id], cancellationToken);
        if (tenant is null)
            return Results.NotFound();

        if (!string.Equals(tenant.Slug, request.Slug, StringComparison.OrdinalIgnoreCase))
        {
            if (await db.Tenants.AnyAsync(t => t.Slug == request.Slug && t.Id != id, cancellationToken))
                return Results.Conflict(new { message = "A tenant with this slug already exists." });
        }

        tenant.Name = request.Name;
        tenant.Slug = request.Slug;
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(tenant));
    }

    private static async Task<IResult> DeleteTenant(
        Guid id,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.FindAsync([id], cancellationToken);
        if (tenant is null)
            return Results.NotFound();

        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetTenantMembers(
        Guid id,
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == id, cancellationToken);
        if (!tenantExists)
            return Results.NotFound();

        if (!IsSystemAdministrator(principal))
        {
            var userId = await GetCurrentUserId(principal, db, cancellationToken);
            if (userId is null)
                return Results.NotFound(new { message = "User profile not found." });

            var isMember = await db.UserTenantMemberships
                .AnyAsync(m => m.UserId == userId.Value && m.TenantId == id, cancellationToken);
            if (!isMember)
                return Results.Forbid();
        }

        var members = await db.UserTenantMemberships
            .Where(m => m.TenantId == id)
            .Include(m => m.User)
            .OrderBy(m => m.User.LastName).ThenBy(m => m.User.FirstName)
            .Select(m => new TenantMemberResponse(
                m.UserId,
                m.User.FirstName,
                m.User.LastName,
                m.User.Email,
                m.User.Role,
                m.JoinedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(members);
    }

    private static async Task<IResult> AddTenantMember(
        Guid id,
        [FromBody] AddTenantMemberRequest request,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == id, cancellationToken);
        if (!tenantExists)
            return Results.NotFound(new { message = "Tenant not found." });

        var userExists = await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
        if (!userExists)
            return Results.NotFound(new { message = "User not found." });

        var alreadyMember = await db.UserTenantMemberships
            .AnyAsync(m => m.UserId == request.UserId && m.TenantId == id, cancellationToken);
        if (alreadyMember)
            return Results.Conflict(new { message = "User is already a member of this tenant." });

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = request.UserId,
            TenantId = id,
            JoinedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/tenants/{id}/members", null);
    }

    private static async Task<IResult> RemoveTenantMember(
        Guid id,
        Guid userId,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var membership = await db.UserTenantMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == id, cancellationToken);

        if (membership is null)
            return Results.NotFound();

        db.UserTenantMemberships.Remove(membership);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetMyTenants(
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserId(principal, db, cancellationToken);
        if (userId is null)
            return Results.NotFound(new { message = "User profile not found." });

        var tenants = await db.UserTenantMemberships
            .Where(m => m.UserId == userId.Value)
            .Include(m => m.Tenant)
            .Where(m => m.Tenant.IsActive)
            .OrderBy(m => m.Tenant.Name)
            .Select(m => new UserTenantResponse(m.TenantId, m.Tenant.Name, m.Tenant.Slug))
            .ToListAsync(cancellationToken);

        return Results.Ok(tenants);
    }

    private static async Task<IResult> SetActiveTenant(
        [FromBody] SetActiveTenantRequest request,
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserId(principal, db, cancellationToken);
        if (userId is null)
            return Results.NotFound(new { message = "User profile not found." });

        var isAdmin = IsSystemAdministrator(principal);

        if (!isAdmin)
        {
            var isMember = await db.UserTenantMemberships
                .AnyAsync(m => m.UserId == userId.Value && m.TenantId == request.TenantId, cancellationToken);
            if (!isMember)
                return Results.Forbid();
        }

        var tenant = await db.Tenants.FindAsync([request.TenantId], cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Results.NotFound(new { message = "Tenant not found or inactive." });

        // Return the tenant info — the frontend will store TenantId and send it via X-Tenant-Id header
        return Results.Ok(new UserTenantResponse(tenant.Id, tenant.Name, tenant.Slug));
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

    private static TenantResponse ToResponse(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt);
}

public record SetActiveTenantRequest(Guid TenantId);
