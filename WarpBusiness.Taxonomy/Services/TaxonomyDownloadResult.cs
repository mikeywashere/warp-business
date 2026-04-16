namespace WarpBusiness.Taxonomy.Services;

public record TaxonomyDownloadResult(
    bool Success,
    string? Version,
    string? Checksum,
    IReadOnlyList<ExternalNodeDto> Nodes,
    string? ErrorMessage);
