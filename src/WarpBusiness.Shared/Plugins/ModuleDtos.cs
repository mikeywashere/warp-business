namespace WarpBusiness.Shared.Plugins;

public record ModuleInfoDto(
    string ModuleId,
    string DisplayName,
    string Version,
    string Description,
    string AssemblyPath,
    bool IsLoaded,
    string? LoadError
);

public record ModuleNavItemDto(
    string Label,
    string Href,
    string? Icon,
    string? RequiredRole,
    int DisplayOrder
);
