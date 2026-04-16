using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Api.Endpoints;

public static class CatalogNotationEndpoints
{
    public static void MapCatalogNotationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog/notations").RequireAuthorization();
        group.MapGet("", GetNotations).WithName("GetCatalogNotations");
        group.MapGet("{id:guid}", GetNotation).WithName("GetCatalogNotation");
        group.MapPost("", CreateNotation).WithName("CreateCatalogNotation");
        group.MapPut("{id:guid}", UpdateNotation).WithName("UpdateCatalogNotation");
        group.MapDelete("{id:guid}", DeleteNotation).WithName("DeleteCatalogNotation");

        // Assign/remove notations on products
        var productNotations = app.MapGroup("/api/catalog/products/{productId:guid}/notations").RequireAuthorization();
        productNotations.MapGet("", GetProductNotations).WithName("GetProductCatalogNotations");
        productNotations.MapPost("{notationId:guid}", AddProductNotation).WithName("AddProductCatalogNotation");
        productNotations.MapDelete("{notationId:guid}", RemoveProductNotation).WithName("RemoveProductCatalogNotation");
    }

    private static async Task<IResult> GetNotations(
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var notations = await db.Notations
            .Where(n => n.TenantId == tenantId.Value)
            .OrderBy(n => n.Name)
            .Select(n => new NotationResponse(n.Id, n.TenantId, n.Name, n.Description, n.Icon, n.IsActive, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(notations);
    }

    private static async Task<IResult> GetNotation(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var notation = await db.Notations
            .Where(n => n.Id == id && n.TenantId == tenantId.Value)
            .Select(n => new NotationResponse(n.Id, n.TenantId, n.Name, n.Description, n.Icon, n.IsActive, n.CreatedAt, n.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return notation is null ? Results.NotFound() : Results.Ok(notation);
    }

    private static async Task<IResult> CreateNotation(
        [FromBody] CreateNotationRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Notation name is required." });

        var notation = new CatalogNotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Notations.Add(notation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A notation with this name already exists." });
        }

        return Results.Created(
            $"/api/catalog/notations/{notation.Id}",
            new NotationResponse(notation.Id, notation.TenantId, notation.Name, notation.Description, notation.Icon, notation.IsActive, notation.CreatedAt, notation.UpdatedAt));
    }

    private static async Task<IResult> UpdateNotation(
        Guid id,
        [FromBody] UpdateNotationRequest request,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var notation = await db.Notations
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId.Value, cancellationToken);
        if (notation is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Notation name is required." });

        notation.Name = request.Name;
        notation.Description = request.Description;
        notation.Icon = request.Icon;
        notation.IsActive = request.IsActive ?? notation.IsActive;
        notation.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "A notation with this name already exists." });
        }

        return Results.Ok(new NotationResponse(notation.Id, notation.TenantId, notation.Name, notation.Description, notation.Icon, notation.IsActive, notation.CreatedAt, notation.UpdatedAt));
    }

    private static async Task<IResult> DeleteNotation(
        Guid id,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var notation = await db.Notations
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId.Value, cancellationToken);
        if (notation is null)
            return Results.NotFound();

        var inUse = await db.ProductNotations.AnyAsync(pn => pn.NotationId == id, cancellationToken);
        if (inUse)
        {
            notation.IsActive = false;
            notation.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Notation is assigned to products and was deactivated instead of deleted." });
        }

        db.Notations.Remove(notation);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetProductNotations(
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

        var notations = await db.ProductNotations
            .Where(pn => pn.ProductId == productId)
            .Include(pn => pn.Notation)
            .OrderBy(pn => pn.Notation.Name)
            .Select(pn => new NotationResponse(
                pn.NotationId, pn.Notation.TenantId, pn.Notation.Name,
                pn.Notation.Description, pn.Notation.Icon, pn.Notation.IsActive, pn.Notation.CreatedAt, pn.Notation.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(notations);
    }

    private static async Task<IResult> AddProductNotation(
        Guid productId,
        Guid notationId,
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

        var notationExists = await db.Notations.AnyAsync(n => n.Id == notationId && n.TenantId == tenantId.Value, cancellationToken);
        if (!notationExists)
            return Results.BadRequest(new { message = "Notation not found in this tenant." });

        var alreadyAssigned = await db.ProductNotations.AnyAsync(pn => pn.ProductId == productId && pn.NotationId == notationId, cancellationToken);
        if (alreadyAssigned)
            return Results.Conflict(new { message = "This notation is already assigned to this product." });

        db.ProductNotations.Add(new ProductNotation { ProductId = productId, NotationId = notationId });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/catalog/products/{productId}/notations/{notationId}", null);
    }

    private static async Task<IResult> RemoveProductNotation(
        Guid productId,
        Guid notationId,
        HttpContext httpContext,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantId = httpContext.Items["TenantId"] as Guid?;
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var pn = await db.ProductNotations
            .FirstOrDefaultAsync(pn => pn.ProductId == productId && pn.NotationId == notationId, cancellationToken);
        if (pn is null)
            return Results.NotFound();

        db.ProductNotations.Remove(pn);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record NotationResponse(
    Guid Id, Guid TenantId, string Name, string? Description, NotationIcon? Icon, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CreateNotationRequest(string Name, string? Description = null, NotationIcon? Icon = null);
public record UpdateNotationRequest(string Name, string? Description = null, NotationIcon? Icon = null, bool? IsActive = null);
