namespace WarpBusiness.Plugin.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    bool IsResolved { get; }
}

/// <summary>
/// Design-time / unit-test stub. Returns Guid.Empty / empty slug / false.
/// Use in IDesignTimeDbContextFactory implementations so EF tooling can instantiate DbContexts.
/// </summary>
public class NullTenantContext : ITenantContext
{
    public static readonly NullTenantContext Instance = new();
    public Guid TenantId => Guid.Empty;
    public string TenantSlug => string.Empty;
    public bool IsResolved => false;
}
