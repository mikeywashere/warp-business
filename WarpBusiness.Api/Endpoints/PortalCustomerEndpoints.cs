using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Models;
using WarpBusiness.Crm.Data;
using WarpBusiness.Crm.Models;
using WarpBusiness.Employees.Data;
using WarpBusiness.Employees.Models;

namespace WarpBusiness.Api.Endpoints;

public static class PortalCustomerEndpoints
{
    private const long MaxLogoSizeBytes = 2_097_152; // 2MB
    private static readonly HashSet<string> AllowedMimeTypes = 
        new() { "image/png", "image/jpeg", "image/webp" };

    public static void MapPortalCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/portal/customers/{id:guid}")
            .RequireAuthorization();

        group.MapGet("profile", GetProfile)
            .WithName("GetCustomerProfile");

        group.MapPut("profile", UpdateProfile)
            .WithName("UpdateCustomerProfile");

        group.MapPost("logo", UploadLogo)
            .WithName("UploadCustomerLogo")
            .Accepts<IFormFile>("multipart/form-data");

        group.MapGet("logo", DownloadLogo)
            .WithName("DownloadCustomerLogo");

        group.MapDelete("logo", DeleteLogo)
            .WithName("DeleteCustomerLogo");

        group.MapGet("assignments", GetAssignments)
            .WithName("GetCustomerAssignments");
    }

    private static async Task<IResult> GetProfile(
        Guid id,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var customerId = ExtractCustomerId(principal);
        if (customerId is null)
            return Results.Unauthorized();

        if (customerId != id)
            return Results.Unauthorized();

        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value && c.IsActive, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        return Results.Ok(ToCustomerDtoWithoutLogo(customer));
    }

    private static async Task<IResult> UpdateProfile(
        Guid id,
        [FromBody] CustomerUpdateDto request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var customerId = ExtractCustomerId(principal);
        if (customerId is null)
            return Results.Unauthorized();

        if (customerId != id)
            return Results.Unauthorized();

        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value && c.IsActive, cancellationToken);

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
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToCustomerDtoWithoutLogo(customer));
    }

    private static async Task<IResult> UploadLogo(
        Guid id,
        IFormFile? logo,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var customerId = ExtractCustomerId(principal);
        if (customerId is null)
            return Results.Unauthorized();

        if (customerId != id)
            return Results.Unauthorized();

        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (logo is null || logo.Length == 0)
            return Results.BadRequest(new { message = "Logo file is required." });

        if (logo.Length > MaxLogoSizeBytes)
            return Results.BadRequest(new { message = $"Logo file size must not exceed 2MB. Current size: {logo.Length} bytes." });

        var mimeType = logo.ContentType?.ToLowerInvariant();
        if (string.IsNullOrEmpty(mimeType) || !AllowedMimeTypes.Contains(mimeType))
            return Results.BadRequest(new { message = "Logo must be PNG, JPG, or WEBP format." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value && c.IsActive, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        using var memoryStream = new MemoryStream();
        await logo.CopyToAsync(memoryStream, cancellationToken);
        customer.Logo = memoryStream.ToArray();
        customer.LogoMimeType = mimeType;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToCustomerDtoWithLogo(customer, mimeType));
    }

    private static async Task<IResult> DownloadLogo(
        Guid id,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var customerId = ExtractCustomerId(principal);
        if (customerId is null)
            return Results.Unauthorized();

        if (customerId != id)
            return Results.Unauthorized();

        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value && c.IsActive, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        if (customer.Logo is null || customer.Logo.Length == 0)
            return Results.NoContent();

        var mimeType = customer.LogoMimeType ?? DetectMimeType(customer.Logo) ?? "image/png";

        return Results.File(customer.Logo, mimeType, fileDownloadName: "logo.bin");
    }

    private static async Task<IResult> DeleteLogo(
        Guid id,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        CrmDbContext crmDb,
        CancellationToken cancellationToken)
    {
        var customerId = ExtractCustomerId(principal);
        if (customerId is null)
            return Results.Unauthorized();

        if (customerId != id)
            return Results.Unauthorized();

        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value && c.IsActive, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        customer.Logo = null;
        customer.LogoMimeType = null;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await crmDb.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetAssignments(
        Guid id,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        CrmDbContext crmDb,
        EmployeeDbContext employeeDb,
        CancellationToken cancellationToken)
    {
        var customerId = ExtractCustomerId(principal);
        if (customerId is null)
            return Results.Unauthorized();

        if (customerId != id)
            return Results.Unauthorized();

        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var customer = await crmDb.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value && c.IsActive, cancellationToken);

        if (customer is null)
            return Results.NotFound();

        var assignments = await crmDb.CustomerEmployees
            .Where(ce => ce.CustomerId == id)
            .Join(
                employeeDb.Employees.Where(e => e.TenantId == tenantId.Value && e.EmploymentStatus == EmploymentStatus.Active),
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

    private static Guid? ExtractCustomerId(ClaimsPrincipal principal)
    {
        var subClaim = principal.FindFirstValue("sub");
        if (string.IsNullOrEmpty(subClaim))
            return null;

        if (Guid.TryParse(subClaim, out var customerId))
            return customerId;

        return null;
    }

    private static CustomerDto ToCustomerDtoWithoutLogo(Customer customer) =>
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
            customer.UpdatedAt,
            Logo: null,
            LogoMimeType: null);

    private static CustomerDto ToCustomerDtoWithLogo(Customer customer, string? mimeType) =>
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
            customer.UpdatedAt,
            Logo: customer.Logo,
            LogoMimeType: mimeType);

    private static string? DetectMimeType(byte[] data)
    {
        if (data.Length < 4)
            return null;

        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        // WebP: RIFF ... WEBP
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "image/webp";

        // SVG: starts with < (after optional whitespace)
        var text = System.Text.Encoding.UTF8.GetString(data.Take(100).ToArray()).TrimStart();
        if (text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return "image/svg+xml";

        return null;
    }
}
