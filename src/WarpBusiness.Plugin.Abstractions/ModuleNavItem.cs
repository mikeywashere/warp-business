namespace WarpBusiness.Plugin.Abstractions;

/// <summary>A navigation entry contributed by a module to the CRM sidebar.</summary>
public record ModuleNavItem(
    string Label,
    string Href,
    string? Icon = null,        // Bootstrap icon class, e.g. "bi-star"
    string? RequiredRole = null, // null = all authenticated users
    int DisplayOrder = 100
);
