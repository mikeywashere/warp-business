namespace WarpBusiness.Plugin.Abstractions;

/// <summary>Runtime metadata about a discovered module. Surfaced via GET /api/modules.</summary>
public record ModuleInfo(
    string ModuleId,
    string DisplayName,
    string Version,
    string Description,
    string AssemblyPath,
    bool IsLoaded,
    string? LoadError
);
