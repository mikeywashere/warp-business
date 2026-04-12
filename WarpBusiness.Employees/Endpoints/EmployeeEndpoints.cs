using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Employees.Data;
using WarpBusiness.Employees.Models;

namespace WarpBusiness.Employees.Endpoints;

public static class EmployeeEndpoints
{
    public static void MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var employees = app.MapGroup("/api/employees")
            .RequireAuthorization();

        employees.MapGet("/", GetAllEmployees)
            .WithName("GetAllEmployees");

        employees.MapGet("/{id:guid}", GetEmployeeById)
            .WithName("GetEmployeeById");

        employees.MapPost("/", CreateEmployee)
            .WithName("CreateEmployee")
            .RequireAuthorization("SystemAdministrator");

        employees.MapPut("/{id:guid}", UpdateEmployee)
            .WithName("UpdateEmployee")
            .RequireAuthorization("SystemAdministrator");

        employees.MapDelete("/{id:guid}", DeleteEmployee)
            .WithName("DeleteEmployee")
            .RequireAuthorization("SystemAdministrator");
    }

    private static async Task<IResult> GetAllEmployees(
        HttpContext httpContext,
        EmployeeDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var employees = await db.Employees
            .Where(e => e.TenantId == tenantId.Value)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Select(e => ToResponse(e))
            .ToListAsync(cancellationToken);

        return Results.Ok(employees);
    }

    private static async Task<IResult> GetEmployeeById(
        Guid id,
        HttpContext httpContext,
        EmployeeDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId.Value, cancellationToken);

        return employee is null ? Results.NotFound() : Results.Ok(ToResponse(employee));
    }

    private static async Task<IResult> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        HttpContext httpContext,
        EmployeeDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        // Check for duplicate email within the tenant
        if (await db.Employees.AnyAsync(
            e => e.Email == request.Email && e.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "An employee with this email already exists in this tenant." });

        // Validate manager exists in the same tenant if specified
        if (request.ManagerId.HasValue)
        {
            var managerExists = await db.Employees.AnyAsync(
                e => e.Id == request.ManagerId.Value && e.TenantId == tenantId.Value, cancellationToken);
            if (!managerExists)
                return Results.BadRequest(new { message = "The specified manager does not exist in this tenant." });
        }

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = await GenerateEmployeeNumber(db, tenantId.Value, cancellationToken),
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
            UserId = request.UserId,
            TenantId = tenantId.Value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/employees/{employee.Id}", ToResponse(employee));
    }

    private static async Task<IResult> UpdateEmployee(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        HttpContext httpContext,
        EmployeeDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId.Value, cancellationToken);

        if (employee is null)
            return Results.NotFound();

        // Check email uniqueness if changed
        if (!string.Equals(employee.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await db.Employees.AnyAsync(
                e => e.Email == request.Email && e.TenantId == tenantId.Value && e.Id != id, cancellationToken))
                return Results.Conflict(new { message = "An employee with this email already exists in this tenant." });
        }

        // Validate manager if specified
        if (request.ManagerId.HasValue)
        {
            if (request.ManagerId.Value == id)
                return Results.BadRequest(new { message = "An employee cannot be their own manager." });

            var managerExists = await db.Employees.AnyAsync(
                e => e.Id == request.ManagerId.Value && e.TenantId == tenantId.Value, cancellationToken);
            if (!managerExists)
                return Results.BadRequest(new { message = "The specified manager does not exist in this tenant." });
        }

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
        employee.UserId = request.UserId;
        employee.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(employee));
    }

    private static async Task<IResult> DeleteEmployee(
        Guid id,
        HttpContext httpContext,
        EmployeeDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId.Value, cancellationToken);

        if (employee is null)
            return Results.NotFound();

        db.Employees.Remove(employee);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
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

    private static EmployeeResponse ToResponse(Employee employee) =>
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
}
