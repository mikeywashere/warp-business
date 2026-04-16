namespace WarpBusiness.Taxonomy.Services;

public record ExternalNodeDto(
    string ExternalId,
    string? ParentExternalId,
    string Name,
    string FullPath,
    int Level,
    bool IsLeaf);
