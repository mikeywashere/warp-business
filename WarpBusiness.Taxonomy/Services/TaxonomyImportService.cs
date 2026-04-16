using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WarpBusiness.Taxonomy.Data;
using WarpBusiness.Taxonomy.Models;

namespace WarpBusiness.Taxonomy.Services;

public class TaxonomyImportService
{
    private static readonly Regex SlugCleanup = new(@"[^a-z0-9\-]+", RegexOptions.Compiled);
    private static readonly Regex SlugHyphens = new(@"-{2,}", RegexOptions.Compiled);

    private readonly TaxonomyDbContext _db;
    private readonly ILogger<TaxonomyImportService> _logger;

    public TaxonomyImportService(TaxonomyDbContext db, ILogger<TaxonomyImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(
        string provider,
        IReadOnlyList<string> externalIds,
        Guid tenantId,
        Guid? targetParentId,
        CancellationToken ct)
    {
        var plan = await BuildImportPlanAsync(provider, externalIds, tenantId, targetParentId, ct);

        if (plan.NewNodes.Count > 0)
        {
            _db.TaxonomyNodes.AddRange(plan.NewNodes);
            await _db.SaveChangesAsync(ct);
        }

        var createdResponses = plan.NewNodes
            .Select(node => MapResponse(node, 0))
            .ToList();

        return new ImportResult(plan.NewNodes.Count, plan.NodesSkipped, createdResponses);
    }

    public async Task<IReadOnlyList<TaxonomyNodeResponse>> PreviewImportAsync(
        string provider,
        IReadOnlyList<string> externalIds,
        Guid tenantId,
        Guid? targetParentId,
        CancellationToken ct)
    {
        var plan = await BuildImportPlanAsync(provider, externalIds, tenantId, targetParentId, ct);
        return plan.NewNodes.Select(node => MapResponse(node, 0)).ToList();
    }

    private async Task<ImportPlan> BuildImportPlanAsync(
        string provider,
        IReadOnlyList<string> externalIds,
        Guid tenantId,
        Guid? targetParentId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new InvalidOperationException("Provider is required.");

        var ids = externalIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            throw new InvalidOperationException("External node IDs are required.");

        var externalNodes = await LoadExternalNodesAsync(provider, ids, ct);
        var missing = ids.Where(id => !externalNodes.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"External nodes not found: {string.Join(", ", missing)}");

        TaxonomyNode? targetParent = null;
        if (targetParentId.HasValue)
        {
            targetParent = await _db.TaxonomyNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == targetParentId.Value && n.TenantId == tenantId, ct);
            if (targetParent is null)
                throw new InvalidOperationException("Target parent not found in this tenant.");
        }

        var existingList = await _db.TaxonomyNodes.AsNoTracking()
            .Where(n => n.TenantId == tenantId
                && n.SourceProvider == provider
                && n.SourceExternalId != null)
            .ToListAsync(ct);

        var existingNodes = existingList.ToDictionary(
            n => n.SourceExternalId!,
            StringComparer.OrdinalIgnoreCase);

        var plannedNodes = new Dictionary<string, TaxonomyNode>(existingNodes, StringComparer.OrdinalIgnoreCase);
        var newNodes = new List<TaxonomyNode>();

        foreach (var externalId in ids)
            EnsurePlannedNode(externalId, targetParent);

        var nodesSkipped = externalNodes.Count - newNodes.Count;
        return new ImportPlan(newNodes, nodesSkipped);

        TaxonomyNode EnsurePlannedNode(string externalId, TaxonomyNode? overrideRootParent)
        {
            if (plannedNodes.TryGetValue(externalId, out var existing))
                return existing;

            if (!externalNodes.TryGetValue(externalId, out var externalNode))
                throw new InvalidOperationException($"External node not found: {externalId}");

            TaxonomyNode? parentNode = null;
            if (!string.IsNullOrWhiteSpace(externalNode.ParentExternalId))
            {
                parentNode = EnsurePlannedNode(externalNode.ParentExternalId, overrideRootParent);
            }
            else if (overrideRootParent is not null)
            {
                parentNode = overrideRootParent;
            }

            var slug = Slugify(externalNode.Name);
            var materializedPath = BuildMaterializedPath(parentNode?.MaterializedPath, slug);
            var level = parentNode?.Level + 1 ?? 0;

            var node = new TaxonomyNode
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ParentNodeId = parentNode?.Id,
                Name = externalNode.Name,
                Description = null,
                Level = level,
                MaterializedPath = materializedPath,
                SortOrder = 0,
                IsActive = true,
                SourceProvider = provider,
                SourceExternalId = externalNode.ExternalId,
                SourcePath = externalNode.FullPath,
                SourceImportedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            plannedNodes[externalId] = node;
            newNodes.Add(node);
            return node;
        }
    }

    private async Task<Dictionary<string, ExternalTaxonomyNode>> LoadExternalNodesAsync(
        string provider,
        IReadOnlyList<string> externalIds,
        CancellationToken ct)
    {
        var nodes = new Dictionary<string, ExternalTaxonomyNode>(StringComparer.OrdinalIgnoreCase);
        var pending = new HashSet<string>(externalIds, StringComparer.OrdinalIgnoreCase);

        while (pending.Count > 0)
        {
            var batch = pending.ToList();
            pending.Clear();

            var fetched = await _db.ExternalTaxonomyNodes.AsNoTracking()
                .Where(n => n.Provider == provider && batch.Contains(n.ExternalId))
                .ToListAsync(ct);

            foreach (var node in fetched)
            {
                if (nodes.TryAdd(node.ExternalId, node) &&
                    !string.IsNullOrWhiteSpace(node.ParentExternalId) &&
                    !nodes.ContainsKey(node.ParentExternalId))
                {
                    pending.Add(node.ParentExternalId);
                }
            }
        }

        return nodes;
    }

    private static TaxonomyNodeResponse MapResponse(TaxonomyNode node, int childCount)
        => new(
            node.Id,
            node.TenantId,
            node.ParentNodeId,
            node.Name,
            node.Description,
            node.Level,
            node.MaterializedPath,
            node.IsActive,
            node.SourceProvider,
            node.SourceExternalId,
            node.SourceImportedAt,
            childCount,
            node.CreatedAt,
            node.UpdatedAt);

    private static string BuildMaterializedPath(string? parentPath, string slug)
        => string.IsNullOrWhiteSpace(parentPath) ? $"/{slug}" : $"{parentPath}/{slug}";

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "node";

        var builder = new StringBuilder();
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else
                builder.Append('-');
        }

        var slug = SlugCleanup.Replace(builder.ToString(), "-");
        slug = SlugHyphens.Replace(slug, "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "node" : slug;
    }

    private sealed record ImportPlan(List<TaxonomyNode> NewNodes, int NodesSkipped);
}
