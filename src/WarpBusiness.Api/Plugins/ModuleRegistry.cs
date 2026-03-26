using WarpBusiness.Plugin.Abstractions;

namespace WarpBusiness.Api.Plugins;

/// <summary>
/// Singleton registry of all loaded modules. Registered in DI so controllers and services can query it.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly List<ICustomModule> _modules;
    private readonly List<ModuleInfo> _moduleInfos;

    public ModuleRegistry(IEnumerable<ICustomModule> modules, IEnumerable<ModuleInfo> moduleInfos)
    {
        _modules = modules.ToList();
        _moduleInfos = moduleInfos.ToList();
    }

    public IReadOnlyList<ICustomModule> Modules => _modules.AsReadOnly();
    public IReadOnlyList<ModuleInfo> ModuleInfos => _moduleInfos.AsReadOnly();

    /// <summary>Returns all nav items from all loaded modules, sorted by DisplayOrder.</summary>
    public IReadOnlyList<ModuleNavItem> GetAllNavItems() =>
        _modules.SelectMany(m => m.GetNavItems())
                .OrderBy(n => n.DisplayOrder)
                .ToList()
                .AsReadOnly();
}
