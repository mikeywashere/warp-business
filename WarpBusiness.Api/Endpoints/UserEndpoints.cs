using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Services;

namespace WarpBusiness.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/users")
            .RequireAuthorization();

        users.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser");

        users.MapPut("/me", UpdateMyProfile)
            .WithName("UpdateMyProfile");

        users.MapGet("/", GetAllUsers)
            .WithName("GetAllUsers");

        users.MapGet("/{id:guid}", GetUserById)
            .WithName("GetUserById")
            .RequireAuthorization("SystemAdministrator");

        users.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .RequireAuthorization("SystemAdministrator");

        users.MapPut("/{id:guid}", UpdateUser)
            .WithName("UpdateUser")
            .RequireAuthorization("SystemAdministrator");

        users.MapDelete("/{id:guid}", DeleteUser)
            .WithName("DeleteUser")
            .RequireAuthorization("SystemAdministrator");
    }

    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal principal,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var subjectId = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);

        ApplicationUser? user = null;

        if (!string.IsNullOrEmpty(subjectId))
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakSubjectId == subjectId, cancellationToken);
        }

        if (user is null && !string.IsNullOrEmpty(email))
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            // Link Keycloak subject ID on first login match
            if (user is not null && !string.IsNullOrEmpty(subjectId) && string.IsNullOrEmpty(user.KeycloakSubjectId))
            {
                user.KeycloakSubjectId = subjectId;
                user.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (user is null)
            return Results.NotFound(new { message = "User profile not found. Contact a System Administrator." });

        return Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> UpdateMyProfile(
        ClaimsPrincipal principal,
        [FromBody] UpdateProfileRequest request,
        WarpBusinessDbContext db,
        KeycloakAdminService keycloakAdmin,
        CancellationToken cancellationToken)
    {
        var subjectId = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);

        ApplicationUser? user = null;

        if (!string.IsNullOrEmpty(subjectId))
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakSubjectId == subjectId, cancellationToken);
        }

        if (user is null && !string.IsNullOrEmpty(email))
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }

        if (user is null)
            return Results.NotFound(new { message = "User profile not found. Contact a System Administrator." });

        // Update in Keycloak if we have their subject ID
        if (!string.IsNullOrEmpty(user.KeycloakSubjectId))
        {
            var keycloakSuccess = await keycloakAdmin.UpdateUserAsync(
                user.KeycloakSubjectId, request.FirstName, request.LastName, user.Email, cancellationToken);
            if (!keycloakSuccess)
                return Results.Problem("Failed to update user in identity provider.");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> GetAllUsers(
        WarpBusinessDbContext db,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var isAdmin = IsSystemAdministrator(principal);
        var tenantId = httpContext.Items["TenantId"] as Guid?;

        // SystemAdministrators with no tenant context: return ALL users with tenant memberships
        if (isAdmin && tenantId is null)
        {
            var users = await db.Users
                .Include(u => u.TenantMemberships)
                    .ThenInclude(m => m.Tenant)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .ToListAsync(cancellationToken);

            var response = users.Select(u => ToResponseWithTenants(u)).ToList();
            return Results.Ok(response);
        }

        // Regular users with no tenant context: return empty list
        if (!isAdmin && tenantId is null)
            return Results.Ok(Array.Empty<UserResponse>());

        // At this point, we either have a tenant context or we need to filter by it
        // With tenant context (admin or regular user): show only tenant members
        if (tenantId.HasValue)
        {
            if (isAdmin)
            {
                // Admins: load users with full tenant memberships
                var adminUsers = await db.UserTenantMemberships
                    .Where(m => m.TenantId == tenantId.Value)
                    .Include(m => m.User)
                        .ThenInclude(u => u.TenantMemberships)
                            .ThenInclude(tm => tm.Tenant)
                    .OrderBy(m => m.User.LastName).ThenBy(m => m.User.FirstName)
                    .Select(m => m.User)
                    .ToListAsync(cancellationToken);

                var adminResponse = adminUsers.Select(u => ToResponseWithTenants(u)).ToList();
                return Results.Ok(adminResponse);
            }

            // Regular users: simple response, only users in their tenant
            var tenantUsers = await db.UserTenantMemberships
                .Where(m => m.TenantId == tenantId.Value)
                .Include(m => m.User)
                .OrderBy(m => m.User.LastName).ThenBy(m => m.User.FirstName)
                .Select(m => ToResponse(m.User))
                .ToListAsync(cancellationToken);

            return Results.Ok(tenantUsers);
        }

        // This should never be reached, but return empty as fallback
        return Results.Ok(Array.Empty<UserResponse>());
    }

    private static async Task<IResult> GetUserById(
        Guid id,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([id], cancellationToken);
        return user is null ? Results.NotFound() : Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> CreateUser(
        [FromBody] CreateUserRequest request,
        WarpBusinessDbContext db,
        KeycloakAdminService keycloakAdmin,
        CancellationToken cancellationToken)
    {
        // Check for duplicate email
        if (await db.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
            return Results.Conflict(new { message = "A user with this email already exists." });

        // Validate tenant exists if provided
        Tenant? tenant = null;
        if (request.TenantId.HasValue)
        {
            tenant = await db.Tenants.FindAsync([request.TenantId.Value], cancellationToken);
            if (tenant == null)
                return Results.BadRequest(new { message = "The specified tenant does not exist." });
        }

        // Create in Keycloak
        var result = await keycloakAdmin.CreateUserAsync(
            request.FirstName, request.LastName, request.Email, request.Password, cancellationToken);

        if (!result.Success)
        {
            var statusCode = (int)(result.StatusCode ?? System.Net.HttpStatusCode.BadGateway);

            // Keycloak 409 Conflict (duplicate in Keycloak but not in our DB)
            if (statusCode == 409)
            {
                return Results.Conflict(new { message = result.ErrorMessage ?? "User already exists in identity provider." });
            }

            // Keycloak 400-level errors are client/validation problems — pass them through as 400
            if (statusCode >= 400 && statusCode < 500)
            {
                return Results.Problem(
                    detail: result.ErrorMessage,
                    title: "User creation failed",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Actual Keycloak server errors → 502
            return Results.Problem(
                detail: result.ErrorMessage ?? "Failed to create user in identity provider.",
                title: "Identity provider error",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubjectId = result.KeycloakUserId!,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Role = request.Role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        // Create tenant membership if tenant was provided
        if (tenant != null)
        {
            var membership = new UserTenantMembership
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                JoinedAt = DateTime.UtcNow
            };
            db.UserTenantMemberships.Add(membership);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Created($"/api/users/{user.Id}", ToResponse(user));
    }

    private static async Task<IResult> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request,
        WarpBusinessDbContext db,
        KeycloakAdminService keycloakAdmin,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null)
            return Results.NotFound();

        // Check email uniqueness if changed
        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await db.Users.AnyAsync(u => u.Email == request.Email && u.Id != id, cancellationToken))
                return Results.Conflict(new { message = "A user with this email already exists." });
        }

        // Update in Keycloak if we have their subject ID
        if (!string.IsNullOrEmpty(user.KeycloakSubjectId))
        {
            var keycloakSuccess = await keycloakAdmin.UpdateUserAsync(
                user.KeycloakSubjectId, request.FirstName, request.LastName, request.Email, cancellationToken);
            if (!keycloakSuccess)
                return Results.Problem("Failed to update user in identity provider.");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Email = request.Email;
        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> DeleteUser(
        Guid id,
        WarpBusinessDbContext db,
        KeycloakAdminService keycloakAdmin,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null)
            return Results.NotFound();

        // Delete from Keycloak if we have their subject ID
        if (!string.IsNullOrEmpty(user.KeycloakSubjectId))
        {
            await keycloakAdmin.DeleteUserAsync(user.KeycloakSubjectId, cancellationToken);
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static UserResponse ToResponse(ApplicationUser user) =>
        new(user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.CreatedAt);

    private static UserWithTenantsResponse ToResponseWithTenants(ApplicationUser user)
    {
        var tenants = user.TenantMemberships
            .Select(m => new UserTenantInfo(m.TenantId, m.Tenant.Name))
            .ToList();

        return new UserWithTenantsResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Role,
            user.CreatedAt,
            tenants);
    }

    private static bool IsSystemAdministrator(ClaimsPrincipal principal)
    {
        if (principal.IsInRole("SystemAdministrator"))
            return true;
        if (principal.HasClaim("roles", "SystemAdministrator"))
            return true;
        return principal.HasClaim("app_role", "SystemAdministrator");
    }
}