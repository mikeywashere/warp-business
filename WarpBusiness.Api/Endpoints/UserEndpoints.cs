using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Services;
using WarpBusiness.Employees.Data;

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
        EmployeeDbContext employeeDb,
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

            var userIds = users.Select(u => u.Id).ToList();
            var linkedEmployees = await employeeDb.Employees
                .Where(e => e.UserId.HasValue && userIds.Contains(e.UserId.Value))
                .Select(e => new { e.UserId, e.Id })
                .ToListAsync(cancellationToken);
            var linkMap = linkedEmployees.ToDictionary(e => e.UserId!.Value, e => e.Id);

            var response = users.Select(u => ToResponseWithTenants(u, linkMap.GetValueOrDefault(u.Id))).ToList();
            return Results.Ok(response);
        }

        // Regular users with no tenant context: return empty list
        if (!isAdmin && tenantId is null)
            return Results.Ok(Array.Empty<UserResponse>());

        // With tenant context (admin or regular user): show only tenant members
        if (tenantId.HasValue)
        {
            if (isAdmin)
            {
                var adminUsers = await db.UserTenantMemberships
                    .Where(m => m.TenantId == tenantId.Value)
                    .Include(m => m.User)
                        .ThenInclude(u => u.TenantMemberships)
                            .ThenInclude(tm => tm.Tenant)
                    .OrderBy(m => m.User.LastName).ThenBy(m => m.User.FirstName)
                    .Select(m => m.User)
                    .ToListAsync(cancellationToken);

                var adminUserIds = adminUsers.Select(u => u.Id).ToList();
                var adminLinked = await employeeDb.Employees
                    .Where(e => e.UserId.HasValue && adminUserIds.Contains(e.UserId.Value))
                    .Select(e => new { e.UserId, e.Id })
                    .ToListAsync(cancellationToken);
                var adminLinkMap = adminLinked.ToDictionary(e => e.UserId!.Value, e => e.Id);

                var adminResponse = adminUsers.Select(u => ToResponseWithTenants(u, adminLinkMap.GetValueOrDefault(u.Id))).ToList();
                return Results.Ok(adminResponse);
            }

            // Regular users: simple response, only users in their tenant
            var tenantUsers = await db.UserTenantMemberships
                .Where(m => m.TenantId == tenantId.Value)
                .Include(m => m.User)
                .OrderBy(m => m.User.LastName).ThenBy(m => m.User.FirstName)
                .Select(m => m.User)
                .ToListAsync(cancellationToken);

            var tenantUserIds = tenantUsers.Select(u => u.Id).ToList();
            var tenantLinked = await employeeDb.Employees
                .Where(e => e.UserId.HasValue && tenantUserIds.Contains(e.UserId.Value))
                .Select(e => new { e.UserId, e.Id })
                .ToListAsync(cancellationToken);
            var tenantLinkMap = tenantLinked.ToDictionary(e => e.UserId!.Value, e => e.Id);

            var tenantResponse = tenantUsers.Select(u => ToResponse(u, tenantLinkMap.GetValueOrDefault(u.Id))).ToList();
            return Results.Ok(tenantResponse);
        }

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
        ILogger<KeycloakAdminService> logger,
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

        string keycloakUserId;

        if (!result.Success)
        {
            var statusCode = (int)(result.StatusCode ?? System.Net.HttpStatusCode.BadGateway);

            // Keycloak 409 Conflict — user exists in Keycloak. Check if it's an orphan (missing from local DB).
            if (statusCode == 409)
            {
                var existingKeycloakUser = await keycloakAdmin.GetUserByEmailAsync(request.Email, cancellationToken);
                if (existingKeycloakUser is null)
                {
                    return Results.Conflict(new { message = result.ErrorMessage ?? "User already exists in identity provider." });
                }

                // Check if this Keycloak user is already linked in our local DB
                var existsInDb = await db.Users.AnyAsync(
                    u => u.KeycloakSubjectId == existingKeycloakUser.Id || u.Email == request.Email,
                    cancellationToken);

                if (existsInDb)
                {
                    return Results.Conflict(new { message = "A user with this email already exists." });
                }

                // Orphaned Keycloak user — adopt it by creating the local DB record
                logger.LogInformation(
                    "Adopting orphaned Keycloak user {KeycloakUserId} for email {Email}",
                    existingKeycloakUser.Id, request.Email);

                keycloakUserId = existingKeycloakUser.Id;
            }
            else if (statusCode >= 400 && statusCode < 500)
            {
                // Keycloak 400-level errors are client/validation problems — pass them through as 400
                return Results.Problem(
                    detail: result.ErrorMessage,
                    title: "User creation failed",
                    statusCode: StatusCodes.Status400BadRequest);
            }
            else
            {
                // Actual Keycloak server errors → 502
                return Results.Problem(
                    detail: result.ErrorMessage ?? "Failed to create user in identity provider.",
                    title: "Identity provider error",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }
        else
        {
            keycloakUserId = result.KeycloakUserId!;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubjectId = keycloakUserId,
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
        EmployeeDbContext employeeDb,
        KeycloakAdminService keycloakAdmin,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null)
            return Results.NotFound();

        // Block deletion if linked to an employee
        var isLinked = await employeeDb.Employees.AnyAsync(e => e.UserId == id, cancellationToken);
        if (isLinked)
            return Results.BadRequest(new { message = "This user is linked to an employee record and cannot be deleted." });

        // Delete from Keycloak if we have their subject ID
        if (!string.IsNullOrEmpty(user.KeycloakSubjectId))
        {
            await keycloakAdmin.DeleteUserAsync(user.KeycloakSubjectId, cancellationToken);
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static UserResponse ToResponse(ApplicationUser user, Guid? linkedEmployeeId = null) =>
        new(user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.CreatedAt, linkedEmployeeId);

    private static UserWithTenantsResponse ToResponseWithTenants(ApplicationUser user, Guid? linkedEmployeeId = null)
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
            tenants,
            linkedEmployeeId);
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