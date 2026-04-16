namespace WarpBusiness.Taxonomy.Models;

public record TaxonomyNodeResponse(
    Guid Id,
    Guid TenantId,
    Guid? ParentNodeId,
    string Name,
    string? Description,
    int Level,
    string MaterializedPath,
    bool IsActive,
    string? SourceProvider,
    string? SourceExternalId,
    DateTime? SourceImportedAt,
    int ChildCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateTaxonomyNodeRequest(
    string Name,
    Guid? ParentNodeId,
    string? Description);

public record UpdateTaxonomyNodeRequest(
    string? Name,
    string? Description,
    bool? IsActive);

public record MoveTaxonomyNodeRequest(Guid? NewParentId);

public record ProviderStatusResponse(
    string Provider,
    bool Enabled,
    bool RequiresApiKey,
    DateTime? LastDownload,
    int NodeCount,
    string? Version,
    ExternalCacheStatus? Status);

public record ExternalNodeResponse(
    string ExternalId,
    string? ParentExternalId,
    string Name,
    string FullPath,
    int Level,
    bool IsLeaf,
    bool IsImported);

public record ImportNodesRequest(
    string Provider,
    IReadOnlyList<string> ExternalIds,
    Guid? TargetParentId);

public record ImportResult(
    int NodesCreated,
    int NodesSkipped,
    IReadOnlyList<TaxonomyNodeResponse> CreatedNodes);
