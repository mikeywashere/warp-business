using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Models;
using WarpBusiness.Api.Services;
using WarpBusiness.Employees.Data;
using WarpBusiness.Employees.Models;

namespace WarpBusiness.Api.Endpoints;

public static class EmployeeUserEndpoints
{
    public static void MapEmployeeUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization();

        group.MapGet("/users/unlinked", GetUnlinkedUsers)
            .WithName("GetUnlinkedUsers")
            .RequireAuthorization("SystemAdministrator");

        group.MapPost("/employees/with-user", CreateEmployeeWithUser)
            .WithName("CreateEmployeeWithUser")
            .RequireAuthorization("SystemAdministrator");

        group.MapPut("/employees/{id:guid}/with-user", UpdateEmployeeWithUser)
            .WithName("UpdateEmployeeWithUser")
            .RequireAuthorization("SystemAdministrator");

        group.MapPut("/employees/{id:guid}/link-user/{userId:guid}", LinkUserToEmployee)
            .WithName("LinkUserToEmployee")
            .RequireAuthorization("SystemAdministrator");

        group.MapGet("/employees/by-user/{userId:guid}", GetEmployeeByUserId)
            .WithName("GetEmployeeByUserId");
    }

    private static async Task<IResult> GetUnlinkedUsers(
        HttpContext httpContext,
        WarpBusinessDbContext db,
        EmployeeDbContext employeeDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        // Get all user IDs that are members of this tenant
        var tenantUserIds = await db.UserTenantMemberships
            .Where(m => m.TenantId == tenantId.Value)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        // Get all UserIds already linked to employees in this tenant
        var linkedUserIds = await employeeDb.Employees
            .Where(e => e.TenantId == tenantId.Value && e.UserId.HasValue)
            .Select(e => e.UserId!.Value)
            .ToListAsync(cancellationToken);

        var linkedSet = linkedUserIds.ToHashSet();

        // Return tenant users not already linked
        var unlinkedUserIds = tenantUserIds.Where(id => !linkedSet.Contains(id)).ToList();

        var users = await db.Users
            .Where(u => unlinkedUserIds.Contains(u.Id))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        var response = users.Select(u => new UserResponse(u.Id, u.FirstName, u.LastName, u.Email, u.Username, u.Role, u.CreatedAt)).ToList();
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateEmployeeWithUser(
        [FromBody] CreateEmployeeWithUserRequest request,
        HttpContext httpContext,
        WarpBusinessDbContext db,
        EmployeeDbContext employeeDb,
        KeycloakAdminService keycloakAdmin,
        ILogger<KeycloakAdminService> logger,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        // Validate employee email uniqueness within tenant
        if (await employeeDb.Employees.AnyAsync(
            e => e.Email == request.Email && e.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "An employee with this email already exists in this tenant." });

        // Validate manager if specified
        if (request.ManagerId.HasValue)
        {
            var managerExists = await employeeDb.Employees.AnyAsync(
                e => e.Id == request.ManagerId.Value && e.TenantId == tenantId.Value, cancellationToken);
            if (!managerExists)
                return Results.BadRequest(new { message = "The specified manager does not exist in this tenant." });
        }

        // Parse role
        if (!Enum.TryParse<UserRole>(request.Role, true, out var userRole))
            return Results.BadRequest(new { message = $"Invalid role: {request.Role}. Valid values: User, SystemAdministrator." });

        // Derive username: use provided value, or extract from email local part
        var username = !string.IsNullOrWhiteSpace(request.Username)
            ? request.Username
            : request.Email.Split('@')[0];

        // Keycloak username is prefixed with tenant ID for uniqueness across tenants
        var keycloakUsername = $"{tenantId.Value}.{username}";

        // Create Keycloak user WITHOUT password
        var kcResult = await keycloakAdmin.CreateUserWithoutPasswordAsync(
            request.FirstName, request.LastName, request.Email, keycloakUsername, cancellationToken);

        string keycloakUserId;

        if (!kcResult.Success)
        {
            var statusCode = (int)(kcResult.StatusCode ?? HttpStatusCode.BadGateway);

            if (statusCode == 409)
            {
                // Orphan recovery: user exists in Keycloak but maybe not in local DB
                var existingKcUser = await keycloakAdmin.GetUserByEmailAsync(request.Email, cancellationToken);
                if (existingKcUser is null)
                    return Results.Conflict(new { message = kcResult.ErrorMessage ?? "User already exists in identity provider." });

                var existsInDb = await db.Users.AnyAsync(
                    u => u.KeycloakSubjectId == existingKcUser.Id || u.Email == request.Email, cancellationToken);
                if (existsInDb)
                    return Results.Conflict(new { message = "A user with this email already exists." });

                logger.LogInformation("Adopting orphaned Keycloak user {KeycloakUserId} for email {Email}",
                    existingKcUser.Id, request.Email);
                keycloakUserId = existingKcUser.Id;
            }
            else if (statusCode >= 400 && statusCode < 500)
            {
                return Results.Problem(
                    detail: kcResult.ErrorMessage,
                    title: "User creation failed",
                    statusCode: StatusCodes.Status400BadRequest);
            }
            else
            {
                return Results.Problem(
                    detail: kcResult.ErrorMessage ?? "Failed to create user in identity provider.",
                    title: "Identity provider error",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }
        else
        {
            keycloakUserId = kcResult.KeycloakUserId!;
        }

        // Create local ApplicationUser
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            KeycloakSubjectId = keycloakUserId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Username = username,
            Role = userRole,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);

        // Create tenant membership
        var tenant = await db.Tenants.FindAsync([tenantId.Value], cancellationToken);
        if (tenant is null)
            return Results.BadRequest(new { message = "The current tenant does not exist." });

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenantId.Value,
            JoinedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);

        // Create Employee linked to the new user
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = await GenerateEmployeeNumber(employeeDb, tenantId.Value, cancellationToken),
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            Email = request.Email,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth,
            HireDate = request.HireDate,
            Department = request.Department,
            JobTitle = request.JobTitle,
            ManagerId = request.ManagerId,
            EmploymentStatus = request.EmploymentStatus,
            EmploymentType = request.EmploymentType,
            UserId = user.Id,
            TenantId = tenantId.Value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        employeeDb.Employees.Add(employee);
        await employeeDb.SaveChangesAsync(cancellationToken);

        // Best-effort: send set-password email
        try
        {
            var emailSent = await keycloakAdmin.SendRequiredActionsEmailAsync(
                keycloakUserId, ["UPDATE_PASSWORD"], cancellationToken);
            if (!emailSent)
                logger.LogWarning("Failed to send set-password email to {Email}, user will need manual password reset.", request.Email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Exception sending set-password email to {Email}", request.Email);
        }

        return Results.Created($"/api/employees/{employee.Id}", ToEmployeeResponse(employee));
    }

    private static async Task<IResult> UpdateEmployeeWithUser(
        Guid id,
        [FromBody] UpdateEmployeeWithUserRequest request,
        HttpContext httpContext,
        WarpBusinessDbContext db,
        EmployeeDbContext employeeDb,
        KeycloakAdminService keycloakAdmin,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var employee = await employeeDb.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId.Value, cancellationToken);

        if (employee is null)
            return Results.NotFound();

        // Check email uniqueness if changed
        if (!string.Equals(employee.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await employeeDb.Employees.AnyAsync(
                e => e.Email == request.Email && e.TenantId == tenantId.Value && e.Id != id, cancellationToken))
                return Results.Conflict(new { message = "An employee with this email already exists in this tenant." });
        }

        // Validate manager if specified
        if (request.ManagerId.HasValue)
        {
            if (request.ManagerId.Value == id)
                return Results.BadRequest(new { message = "An employee cannot be their own manager." });

            var managerExists = await employeeDb.Employees.AnyAsync(
                e => e.Id == request.ManagerId.Value && e.TenantId == tenantId.Value, cancellationToken);
            if (!managerExists)
                return Results.BadRequest(new { message = "The specified manager does not exist in this tenant." });
        }

        // Update employee fields
        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.MiddleName = request.MiddleName;
        employee.Email = request.Email;
        employee.Phone = request.Phone;
        employee.DateOfBirth = request.DateOfBirth;
        employee.HireDate = request.HireDate;
        employee.TerminationDate = request.TerminationDate;
        employee.Department = request.Department;
        employee.JobTitle = request.JobTitle;
        employee.ManagerId = request.ManagerId;
        employee.EmploymentStatus = request.EmploymentStatus;
        employee.EmploymentType = request.EmploymentType;
        employee.UpdatedAt = DateTime.UtcNow;

        // If employee has a linked user, update the user too
        if (employee.UserId.HasValue)
        {
            var user = await db.Users.FindAsync([employee.UserId.Value], cancellationToken);
            if (user is not null)
            {
                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.Email = request.Email;

                if (!string.IsNullOrEmpty(request.Role) && Enum.TryParse<UserRole>(request.Role, true, out var userRole))
                    user.Role = userRole;

                user.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                // Update Keycloak
                if (!string.IsNullOrEmpty(user.KeycloakSubjectId))
                {
                    await keycloakAdmin.UpdateUserAsync(
                        user.KeycloakSubjectId, request.FirstName, request.LastName, request.Email, cancellationToken);
                }
            }
        }

        await employeeDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToEmployeeResponse(employee));
    }

    private static async Task<IResult> GetEmployeeByUserId(
        Guid userId,
        HttpContext httpContext,
        EmployeeDbContext employeeDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;

        Employee? employee;
        if (tenantId.HasValue)
        {
            employee = await employeeDb.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId && e.TenantId == tenantId.Value, cancellationToken);
        }
        else
        {
            employee = await employeeDb.Employees
                .FirstOrDefaultAsync(e => e.UserId == userId, cancellationToken);
        }

        return employee is null ? Results.NotFound() : Results.Ok(ToEmployeeResponse(employee));
    }

    private static async Task<IResult> LinkUserToEmployee(
        Guid id,
        Guid userId,
        HttpContext httpContext,
        WarpBusinessDbContext db,
        EmployeeDbContext employeeDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var employee = await employeeDb.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId.Value, cancellationToken);

        if (employee is null)
            return Results.NotFound(new { message = "Employee not found." });

        if (employee.UserId.HasValue)
            return Results.BadRequest(new { message = "Employee is already linked to a user account." });

        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user is null)
            return Results.BadRequest(new { message = "User not found." });

        var userBelongsToTenant = await db.UserTenantMemberships.AnyAsync(
            m => m.UserId == userId && m.TenantId == tenantId.Value, cancellationToken);
        if (!userBelongsToTenant)
            return Results.BadRequest(new { message = "User is not a member of this tenant." });

        var userAlreadyLinked = await employeeDb.Employees.AnyAsync(
            e => e.UserId == userId && e.TenantId == tenantId.Value, cancellationToken);
        if (userAlreadyLinked)
            return Results.Conflict(new { message = "User is already linked to another employee." });

        SyncMissingDataFromUserToEmployee(user, employee);

        employee.UserId = userId;
        employee.UpdatedAt = DateTime.UtcNow;

        await employeeDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToEmployeeResponse(employee));
    }

    private static EmployeeResponse ToEmployeeResponse(Employee employee) =>
        new(
            employee.Id,
            employee.EmployeeNumber,
            employee.FirstName,
            employee.LastName,
            employee.MiddleName,
            employee.Email,
            employee.Phone,
            employee.DateOfBirth,
            employee.HireDate,
            employee.TerminationDate,
            employee.Department,
            employee.JobTitle,
            employee.ManagerId,
            employee.EmploymentStatus,
            employee.EmploymentType,
            employee.UserId,
            employee.TenantId,
            employee.CreatedAt,
            employee.UpdatedAt);

    /// <summary>
    /// Syncs missing data from an ApplicationUser to an Employee.
    /// Only populates empty employee fields from the corresponding user fields.
    /// </summary>
    private static void SyncMissingDataFromUserToEmployee(ApplicationUser user, Employee employee)
    {
        if (string.IsNullOrWhiteSpace(employee.FirstName) && !string.IsNullOrWhiteSpace(user.FirstName))
            employee.FirstName = user.FirstName;

        if (string.IsNullOrWhiteSpace(employee.LastName) && !string.IsNullOrWhiteSpace(user.LastName))
            employee.LastName = user.LastName;

        if (string.IsNullOrWhiteSpace(employee.Email) && !string.IsNullOrWhiteSpace(user.Email))
            employee.Email = user.Email;
    }

    private static async Task<string> GenerateEmployeeNumber(
        EmployeeDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var maxNumber = await db.Employees
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.EmployeeNumber)
            .MaxAsync(cancellationToken) as string;

        if (maxNumber is not null && maxNumber.StartsWith("EMP") &&
            int.TryParse(maxNumber.AsSpan(3), out var lastNumber))
        {
            return $"EMP{lastNumber + 1:D5}";
        }

        return "EMP00001";
    }
}
