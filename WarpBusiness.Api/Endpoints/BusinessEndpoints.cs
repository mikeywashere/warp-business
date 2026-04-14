using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Crm.Data;
using WarpBusiness.Crm.Models;

namespace WarpBusiness.Api.Endpoints;

public static class BusinessEndpoints
{
    public static void MapBusinessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/crm/businesses")
            .RequireAuthorization();

        group.MapGet("", GetBusinesses)
            .WithName("GetBusinesses");

        group.MapGet("{id:guid}", GetBusiness)
            .WithName("GetBusiness");

        group.MapPost("", CreateBusiness)
            .WithName("CreateBusiness");

        group.MapPut("{id:guid}", UpdateBusiness)
            .WithName("UpdateBusiness");

        group.MapDelete("{id:guid}", DeleteBusiness)
            .WithName("DeleteBusiness");
    }

    private static async Task<IResult> GetBusinesses(
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var businesses = await crmDb.Businesses
            .Where(b => b.TenantId == tenantId.Value)
            .OrderBy(b => b.Name)
            .Select(b => new BusinessResponse(
                b.Id,
                b.TenantId,
                b.Name,
                b.Industry,
                b.Website,
                b.Phone,
                b.Address,
                b.City,
                b.State,
                b.PostalCode,
                b.Country,
                b.Notes,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt,
                b.Customers.Count()))
            .ToListAsync(cancellationToken);

        return Results.Ok(businesses);
    }

    private static async Task<IResult> GetBusiness(
        Guid id,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var business = await crmDb.Businesses
            .Where(b => b.Id == id && b.TenantId == tenantId.Value)
            .Select(b => new BusinessResponse(
                b.Id,
                b.TenantId,
                b.Name,
                b.Industry,
                b.Website,
                b.Phone,
                b.Address,
                b.City,
                b.State,
                b.PostalCode,
                b.Country,
                b.Notes,
                b.IsActive,
                b.CreatedAt,
                b.UpdatedAt,
                b.Customers.Count()))
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
            return Results.NotFound();

        return Results.Ok(business);
    }

    private static async Task<IResult> CreateBusiness(
        [FromBody] CreateBusinessRequest request,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Business name is required." });

        // Check name uniqueness within tenant
        if (await crmDb.Businesses.AnyAsync(
            b => b.Name == request.Name && b.TenantId == tenantId.Value, cancellationToken))
            return Results.Conflict(new { message = "A business with this name already exists in this tenant." });

        var business = new Business
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            Industry = request.Industry,
            Website = request.Website,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            Notes = request.Notes,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        crmDb.Businesses.Add(business);
        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/crm/businesses/{business.Id}",
            new BusinessResponse(
                business.Id,
                business.TenantId,
                business.Name,
                business.Industry,
                business.Website,
                business.Phone,
                business.Address,
                business.City,
                business.State,
                business.PostalCode,
                business.Country,
                business.Notes,
                business.IsActive,
                business.CreatedAt,
                business.UpdatedAt,
                0));
    }

    private static async Task<IResult> UpdateBusiness(
        Guid id,
        [FromBody] UpdateBusinessRequest request,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var business = await crmDb.Businesses
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId.Value, cancellationToken);

        if (business is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Business name is required." });

        // Check name uniqueness if changed
        if (!string.Equals(business.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (await crmDb.Businesses.AnyAsync(
                b => b.Name == request.Name && b.TenantId == tenantId.Value && b.Id != id, cancellationToken))
                return Results.Conflict(new { message = "A business with this name already exists in this tenant." });
        }

        business.Name = request.Name;
        business.Industry = request.Industry;
        business.Website = request.Website;
        business.Phone = request.Phone;
        business.Address = request.Address;
        business.City = request.City;
        business.State = request.State;
        business.PostalCode = request.PostalCode;
        business.Country = request.Country;
        business.Notes = request.Notes;
        business.IsActive = request.IsActive ?? business.IsActive;
        business.UpdatedAt = DateTimeOffset.UtcNow;

        await crmDb.SaveChangesAsync(cancellationToken);

        var customerCount = await crmDb.Customers.CountAsync(c => c.BusinessId == id, cancellationToken);

        return Results.Ok(new BusinessResponse(
            business.Id,
            business.TenantId,
            business.Name,
            business.Industry,
            business.Website,
            business.Phone,
            business.Address,
            business.City,
            business.State,
            business.PostalCode,
            business.Country,
            business.Notes,
            business.IsActive,
            business.CreatedAt,
            business.UpdatedAt,
            customerCount));
    }

    private static async Task<IResult> DeleteBusiness(
        Guid id,
        [FromQuery] bool unlinkCustomers,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var business = await crmDb.Businesses
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId.Value, cancellationToken);

        if (business is null)
            return Results.NotFound();

        var linkedCustomersCount = await crmDb.Customers
            .CountAsync(c => c.BusinessId == id, cancellationToken);

        if (linkedCustomersCount > 0 && !unlinkCustomers)
        {
            return Results.Conflict(new
            {
                message = $"Cannot delete business. {linkedCustomersCount} customer(s) are linked to this business. " +
                         "Set ?unlinkCustomers=true to unlink them first."
            });
        }

        if (unlinkCustomers)
        {
            var linkedCustomers = await crmDb.Customers
                .Where(c => c.BusinessId == id)
                .ToListAsync(cancellationToken);

            foreach (var customer in linkedCustomers)
            {
                customer.BusinessId = null;
            }

            await crmDb.SaveChangesAsync(cancellationToken);
        }

        crmDb.Businesses.Remove(business);
        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

public record BusinessResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Industry,
    string? Website,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CustomerCount);

public record CreateBusinessRequest(
    string Name,
    string? Industry,
    string? Website,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Notes);

public record UpdateBusinessRequest(
    string Name,
    string? Industry,
    string? Website,
    string? Phone,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Notes,
    bool? IsActive);
