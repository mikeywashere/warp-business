using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Models;
using WarpBusiness.Crm.Data;
using WarpBusiness.Crm.Models;
using WarpBusiness.Employees.Data;

namespace WarpBusiness.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers")
            .RequireAuthorization();

        group.MapGet("", GetCustomers)
            .WithName("GetCustomers");

        group.MapGet("{id:guid}", GetCustomer)
            .WithName("GetCustomer");

        group.MapPost("", CreateCustomer)
            .WithName("CreateCustomer");

        group.MapPut("{id:guid}", UpdateCustomer)
            .WithName("UpdateCustomer");

        group.MapPost("{id:guid}/deactivate", DeactivateCustomer)
            .WithName("DeactivateCustomer");

        group.MapPost("{id:guid}/activate", ActivateCustomer)
            .WithName("ActivateCustomer");

        group.MapGet("{id:guid}/employees", GetCustomerEmployees)
            .WithName("GetCustomerEmployees");

        group.MapPost("{id:guid}/employees/{employeeId:guid}", AssignEmployee)
            .WithName("AssignEmployee");

        group.MapPut("{id:guid}/employees/{employeeId:guid}", UpdateEmployeeRelationship)
            .WithName("UpdateEmployeeRelationship");

        group.MapDelete("{id:guid}/employees/{employeeId:guid}", UnassignEmployee)
            .WithName("UnassignEmployee");
    }

    private static async Task<IResult> GetCustomers(
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customers = await crmDb.Customers
            .Where(c => c.TenantId == tenantId.Value)
            .OrderBy(c => c.Name)
            .Select(c => ToCustomerDto(c))
            .ToListAsync(cancellationToken);

        return Results.Ok(customers);
    }

    private static async Task<IResult> GetCustomer(
        Guid id,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        return Results.Ok(ToCustomerDto(customer));
    }

    private static async Task<IResult> CreateCustomer(
        [FromBody] CustomerCreateDto request,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Customer name is required." });

        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest(new { message = "Customer email is required." });

        // Check email uniqueness within tenant
        if (await crmDb.Customers.AnyAsync(
            c => c.Email == request.Email && c.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "A customer with this email already exists in this tenant." });

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            Industry = request.Industry,
            CompanySize = request.CompanySize,
            Website = request.Website,
            Notes = request.Notes,
            IsActive = true,
            Currency = request.Currency ?? "USD",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        crmDb.Customers.Add(customer);
        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/customers/{customer.Id}", ToCustomerDto(customer));
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id,
        [FromBody] CustomerUpdateDto request,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Customer name is required." });

        // Check email uniqueness if changed
        if (!string.Equals(customer.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await crmDb.Customers.AnyAsync(
                c => c.Email == request.Email && c.TenantId == tenantId.Value && c.Id != id, cancellationToken))
                return Results.Conflict(new { message = "A customer with this email already exists in this tenant." });
        }

        customer.Name = request.Name;
        customer.Email = request.Email ?? customer.Email;
        customer.Phone = request.Phone ?? customer.Phone;
        customer.Address = request.Address ?? customer.Address;
        customer.City = request.City ?? customer.City;
        customer.State = request.State ?? customer.State;
        customer.PostalCode = request.PostalCode ?? customer.PostalCode;
        customer.Country = request.Country ?? customer.Country;
        customer.Industry = request.Industry ?? customer.Industry;
        customer.CompanySize = request.CompanySize ?? customer.CompanySize;
        customer.Website = request.Website ?? customer.Website;
        customer.Notes = request.Notes ?? customer.Notes;
        if (!string.IsNullOrWhiteSpace(request.Currency))
            customer.Currency = request.Currency;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToCustomerDto(customer));
    }

    private static async Task<IResult> DeactivateCustomer(
        Guid id,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        customer.IsActive = false;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToCustomerDto(customer));
    }

    private static async Task<IResult> ActivateCustomer(
        Guid id,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        customer.IsActive = true;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToCustomerDto(customer));
    }

    private static async Task<IResult> GetCustomerEmployees(
        Guid id,
        HttpContext httpContext,
        CrmDbContext crmDb,
        EmployeeDbContext employeeDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        var assignments = await crmDb.CustomerEmployees
            .Where(ce => ce.CustomerId == id)
            .Join(
                employeeDb.Employees.Where(e => e.TenantId == tenantId.Value),
                ce => ce.EmployeeId,
                e => e.Id,
                (ce, e) => new CustomerEmployeeDto(
                    ce.CustomerId,
                    ce.EmployeeId,
                    $"{e.FirstName} {e.LastName}",
                    e.Email,
                    ce.Relationship,
                    ce.BillingRate,
                    ce.BillingCurrency,
                    ce.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(assignments);
    }

    private static async Task<IResult> AssignEmployee(
        Guid id,
        Guid employeeId,
        [FromBody] EmployeeAssignmentDto request,
        HttpContext httpContext,
        CrmDbContext crmDb,
        EmployeeDbContext employeeDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Relationship))
            return Results.BadRequest(new { message = "Relationship is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);

        if (customer is null)
            return Results.NotFound(new { message = "Customer not found." });

        var employee = await employeeDb.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId.Value, cancellationToken);

        if (employee is null)
            return Results.NotFound(new { message = "Employee not found." });

        // Check if assignment already exists
        if (await crmDb.CustomerEmployees.AnyAsync(
            ce => ce.CustomerId == id && ce.EmployeeId == employeeId, cancellationToken))
            return Results.Conflict(new { message = "Employee is already assigned to this customer." });

        var billingCurrency = request.BillingCurrency ?? customer.Currency;
        
        var assignment = new CustomerEmployee
        {
            Id = Guid.NewGuid(),
            CustomerId = id,
            EmployeeId = employeeId,
            Relationship = request.Relationship,
            BillingRate = request.BillingRate,
            BillingCurrency = billingCurrency,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        crmDb.CustomerEmployees.Add(assignment);
        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/customers/{id}/employees/{employeeId}",
            new CustomerEmployeeDto(
                id,
                employeeId,
                $"{employee.FirstName} {employee.LastName}",
                employee.Email,
                request.Relationship,
                request.BillingRate,
                billingCurrency,
                assignment.CreatedAt));
    }

    private static async Task<IResult> UpdateEmployeeRelationship(
        Guid id,
        Guid employeeId,
        [FromBody] EmployeeAssignmentDto request,
        HttpContext httpContext,
        CrmDbContext crmDb,
        EmployeeDbContext employeeDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Relationship))
            return Results.BadRequest(new { message = "Relationship is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);

        if (customer is null)
            return Results.NotFound(new { message = "Customer not found." });

        var assignment = await crmDb.CustomerEmployees
            .FirstOrDefaultAsync(ce => ce.CustomerId == id && ce.EmployeeId == employeeId, cancellationToken);

        if (assignment is null)
            return Results.NotFound(new { message = "Employee is not assigned to this customer." });

        assignment.Relationship = request.Relationship;
        if (request.BillingRate.HasValue)
            assignment.BillingRate = request.BillingRate.Value;
        if (!string.IsNullOrWhiteSpace(request.BillingCurrency))
            assignment.BillingCurrency = request.BillingCurrency;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;

        await crmDb.SaveChangesAsync(cancellationToken);

        var employee = await employeeDb.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        return Results.Ok(new CustomerEmployeeDto(
            id,
            employeeId,
            employee is not null ? $"{employee.FirstName} {employee.LastName}" : "Unknown",
            employee?.Email ?? "",
            request.Relationship,
            assignment.BillingRate,
            assignment.BillingCurrency,
            assignment.CreatedAt));
    }

    private static async Task<IResult> UnassignEmployee(
        Guid id,
        Guid employeeId,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value, cancellationToken);

        if (customer is null)
            return Results.NotFound(new { message = "Customer not found." });

        var assignment = await crmDb.CustomerEmployees
            .FirstOrDefaultAsync(ce => ce.CustomerId == id && ce.EmployeeId == employeeId, cancellationToken);

        if (assignment is null)
            return Results.NotFound(new { message = "Employee is not assigned to this customer." });

        crmDb.CustomerEmployees.Remove(assignment);
        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static CustomerDto ToCustomerDto(Customer customer) =>
        new(
            customer.Id,
            customer.TenantId,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.State,
            customer.PostalCode,
            customer.Country,
            customer.Industry,
            customer.CompanySize,
            customer.Website,
            customer.Notes,
            customer.IsActive,
            customer.Currency,
            customer.CreatedAt,
            customer.UpdatedAt);
}
