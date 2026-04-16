using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Taxonomy.Data;
using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Api.Endpoints;

public static class TaxonomyNodeEndpoints
{
    public static void MapTaxonomyNodeEndpoints(this WebApplication app)
    {
        var nodes = app.MapGroup("/api/taxonomy/nodes").RequireAuthorization();
        nodes.MapGet("roots", GetRootNodes).WithName("GetTaxonomyRootNodes");
        nodes.MapGet("{id:guid}/children", GetNodeChildren).WithName("GetTaxonomyNodeChildren");
        nodes.MapDelete("{id:guid}", DeleteTaxonomyBranchNode).WithName("DeleteTaxonomyBranchNode");
    }

    private static async Task<IResult> GetRootNodes(
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var nodes = await db.TaxonomyNodes
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId.Value && n.ParentNodeId == null)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Name)
            .Select(n => new TaxonomyNodeResponse(
                n.Id,
                n.TenantId,
                n.ParentNodeId,
                n.Name,
                n.Description,
                n.Level,
                n.MaterializedPath,
                n.IsActive,
                n.SourceProvider,
                n.SourceExternalId,
                n.SourceImportedAt,
                db.TaxonomyNodes.Count(c => c.ParentNodeId == n.Id && c.TenantId == tenantId.Value),
                n.CreatedAt,
                n.UpdatedAt))
            .ToListAsync(ct);

        return Results.Ok(nodes);
    }

    private static async Task<IResult> GetNodeChildren(
        Guid id,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var parentExists = await db.TaxonomyNodes.AsNoTracking()
            .AnyAsync(n => n.Id == id && n.TenantId == tenantId.Value, ct);
        if (!parentExists)
            return Results.NotFound();

        var children = await db.TaxonomyNodes
            .AsNoTracking()
            .Where(n => n.ParentNodeId == id && n.TenantId == tenantId.Value)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Name)
            .Select(n => new TaxonomyNodeResponse(
                n.Id,
                n.TenantId,
                n.ParentNodeId,
                n.Name,
                n.Description,
                n.Level,
                n.MaterializedPath,
                n.IsActive,
                n.SourceProvider,
                n.SourceExternalId,
                n.SourceImportedAt,
                db.TaxonomyNodes.Count(c => c.ParentNodeId == n.Id && c.TenantId == tenantId.Value),
                n.CreatedAt,
                n.UpdatedAt))
            .ToListAsync(ct);

        return Results.Ok(children);
    }

    private static async Task<IResult> DeleteTaxonomyBranchNode(
        Guid id,
        HttpContext httpContext,
        TaxonomyDbContext db,
        CatalogDbContext catalogDb,
        CancellationToken ct,
        bool cascade = false)
    {
        var tenantId = GetTenantId(httpContext);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

        var node = await db.TaxonomyNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId.Value, ct);
        if (node is null)
            return Results.NotFound();

        // Load the node plus all descendants via MaterializedPath prefix
        var subtree = await db.TaxonomyNodes
            .Where(n => n.TenantId == tenantId.Value
                && (n.Id == id || n.MaterializedPath.StartsWith(node.MaterializedPath + "/")))
            .ToListAsync(ct);

        var hasChildren = subtree.Count > 1;

        if (!cascade && hasChildren)
            return Results.BadRequest(new
            {
                error = "Node has children. Use cascade=true to delete the entire branch."
            });

        var subtreeIds = subtree.Select(n => n.Id).ToList();

        // Catalog reference safety check
        var conflictingNodeIds = await GetCatalogConflictsAsync(catalogDb, subtreeIds, ct);
        if (conflictingNodeIds.Count > 0)
            return Results.Conflict(new
            {
                error = "Cannot delete: taxonomy nodes are in use by catalog items",
                conflictingNodeIds
            });

        db.TaxonomyNodes.RemoveRange(subtree);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>
    /// Returns taxonomy node IDs from the given set that are referenced by catalog entities.
    /// Stub implementation: the catalog has no TaxonomyNodeId FK yet.
    /// </summary>
    private static Task<List<Guid>> GetCatalogConflictsAsync(
        CatalogDbContext catalogDb,
        IReadOnlyList<Guid> nodeIds,
        CancellationToken ct)
    {
        // TODO: When catalog gains a TaxonomyNodeId FK, query catalogDb here
        // e.g. return catalogDb.Products.Where(p => nodeIds.Contains(p.TaxonomyNodeId!.Value))
        //           .Select(p => p.TaxonomyNodeId!.Value).Distinct().ToListAsync(ct);
        return Task.FromResult(new List<Guid>());
    }

    private static Guid? GetTenantId(HttpContext httpContext)
        => httpContext.Items["TenantId"] as Guid?;
}
