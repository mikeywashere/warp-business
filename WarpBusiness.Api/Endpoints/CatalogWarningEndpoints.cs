using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Api.Endpoints;

public static class CatalogWarningEndpoints
{
    public static void MapCatalogWarningEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog/warnings").RequireAuthorization();
        group.MapGet("", GetWarnings).WithName("GetCatalogWarnings");
        group.MapGet("{id:guid}", GetWarning).WithName("GetCatalogWarning");
        group.MapPost("", CreateWarning).WithName("CreateCatalogWarning");
        group.MapPut("{id:guid}", UpdateWarning).WithName("UpdateCatalogWarning");
        group.MapDelete("{id:guid}", DeleteWarning).WithName("DeleteCatalogWarning");

        // Assign/remove warnings on products
        var productWarnings = app.MapGroup("/api/catalog/products/{productId:guid}/warnings").RequireAuthorization();
        productWarnings.MapGet("", GetProductWarnings).WithName("GetProductCatalogWarnings");
        productWarnings.MapPost("{warningId:guid}", AddProductWarning).WithName("AddProductCatalogWarning");
        productWarnings.MapDelete("{warningId:guid}", RemoveProductWarning).WithName("RemoveProductCatalogWarning");
    }

    private static async Task<IResult> GetWarnings(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var warnings = await db.Warnings
            .Where(w => w.TenantId == tenantId.Value)
            .OrderBy(w => w.Name)
            .Select(w => new WarningResponse(w.Id, w.TenantId, w.Name, w.Description, w.IsActive, w.CreatedAt, w.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(warnings);
    }

    private static async Task<IResult> GetWarning(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var warning = await db.Warnings
            .Where(w => w.Id == id && w.TenantId == tenantId.Value)
            .Select(w => new WarningResponse(w.Id, w.TenantId, w.Name, w.Description, w.IsActive, w.CreatedAt, w.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return warning is null ? Results.NotFound() : Results.Ok(warning);
    }

    private static async Task<IResult> CreateWarning(
        [FromBody] CreateWarningRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Warning name is required." });

        var warning = new CatalogWarning
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Warnings.Add(warning);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A warning with this name already exists." });
        }

        return Results.Created(
            $"/api/catalog/warnings/{warning.Id}",
            new WarningResponse(warning.Id, warning.TenantId, warning.Name, warning.Description, warning.IsActive, warning.CreatedAt, warning.UpdatedAt));
    }

    private static async Task<IResult> UpdateWarning(
        Guid id,
        [FromBody] UpdateWarningRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var warning = await db.Warnings
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId.Value, cancellationToken);
        if (warning is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Warning name is required." });

        warning.Name = request.Name;
        warning.Description = request.Description;
        warning.IsActive = request.IsActive ?? warning.IsActive;
        warning.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A warning with this name already exists." });
        }

        return Results.Ok(new WarningResponse(warning.Id, warning.TenantId, warning.Name, warning.Description, warning.IsActive, warning.CreatedAt, warning.UpdatedAt));
    }

    private static async Task<IResult> DeleteWarning(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var warning = await db.Warnings
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId.Value, cancellationToken);
        if (warning is null)
            return Results.NotFound();

        var inUse = await db.ProductWarnings.AnyAsync(pw => pw.WarningId == id, cancellationToken);
        if (inUse)
        {
            warning.IsActive = false;
            warning.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Warning is assigned to products and was deactivated instead of deleted." });
        }

        db.Warnings.Remove(warning);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetProductWarnings(
        Guid productId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productExists = await db.Products.AnyAsync(p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (!productExists)
            return Results.NotFound();

        var warnings = await db.ProductWarnings
            .Where(pw => pw.ProductId == productId)
            .Include(pw => pw.Warning)
            .OrderBy(pw => pw.Warning.Name)
            .Select(pw => new WarningResponse(
                pw.WarningId, pw.Warning.TenantId, pw.Warning.Name,
                pw.Warning.Description, pw.Warning.IsActive, pw.Warning.CreatedAt, pw.Warning.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(warnings);
    }

    private static async Task<IResult> AddProductWarning(
        Guid productId,
        Guid warningId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var productExists = await db.Products.AnyAsync(p => p.Id == productId && p.TenantId == tenantId.Value, cancellationToken);
        if (!productExists)
            return Results.NotFound();

        var warningExists = await db.Warnings.AnyAsync(w => w.Id == warningId && w.TenantId == tenantId.Value, cancellationToken);
        if (!warningExists)
            return Results.BadRequest(new { message = "Warning not found in this tenant." });

        var alreadyAssigned = await db.ProductWarnings.AnyAsync(pw => pw.ProductId == productId && pw.WarningId == warningId, cancellationToken);
        if (alreadyAssigned)
            return Results.Conflict(new { message = "This warning is already assigned to this product." });

        db.ProductWarnings.Add(new ProductWarning { ProductId = productId, WarningId = warningId });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/catalog/products/{productId}/warnings/{warningId}", null);
    }

    private static async Task<IResult> RemoveProductWarning(
        Guid productId,
        Guid warningId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var pw = await db.ProductWarnings
            .FirstOrDefaultAsync(pw => pw.ProductId == productId && pw.WarningId == warningId, cancellationToken);
        if (pw is null)
            return Results.NotFound();

        db.ProductWarnings.Remove(pw);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record WarningResponse(
    Guid Id, Guid TenantId, string Name, string? Description, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateWarningRequest(string Name, string? Description = null);
public record UpdateWarningRequest(string Name, string? Description = null, bool? IsActive = null);
