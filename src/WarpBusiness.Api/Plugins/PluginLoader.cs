using System.Reflection;
using Microsoft.Extensions.Logging;
using WarpBusiness.Plugin.Abstractions;

namespace WarpBusiness.Api.Plugins;

/// <summary>
/// Discovers and loads ICustomModule implementations from DLL files in the plugins directory.
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly string _pluginsDirectory;
    private readonly List<(ICustomModule Module, ModuleInfo Info)> _loaded = [];

    public IReadOnlyList<(ICustomModule Module, ModuleInfo Info)> LoadedModules => _loaded.AsReadOnly();

    public PluginLoader(ILogger<PluginLoader> logger, string pluginsDirectory)
    {
        _logger = logger;
        _pluginsDirectory = pluginsDirectory;
    }

    /// <summary>
    /// Scans the plugins directory and loads all valid ICustomModule implementations.
    /// Errors in one plugin are caught and logged; they do not abort loading of others.
    /// </summary>
    public void DiscoverAndLoad()
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            _logger.LogInformation("Plugin directory {Dir} does not exist — no plugins loaded.", _pluginsDirectory);
            return;
        }

        var dlls = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        _logger.LogInformation("Scanning {Count} DLL(s) in {Dir}.", dlls.Length, _pluginsDirectory);

        foreach (var dllPath in dlls)
        {
            TryLoadPlugin(dllPath);
        }

        _logger.LogInformation("{Count} plugin(s) loaded successfully.", _loaded.Count);
    }

    private void TryLoadPlugin(string dllPath)
    {
        string? moduleId = null;
        try
        {
            var context = new ModuleAssemblyLoadContext(dllPath);
            var assembly = context.LoadFromAssemblyPath(dllPath);

            var moduleTypes = assembly.GetExportedTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ICustomModule).IsAssignableFrom(t))
                .ToList();

            if (moduleTypes.Count == 0)
            {
                _logger.LogDebug("No ICustomModule implementations found in {File} — skipped.", Path.GetFileName(dllPath));
                return;
            }

            foreach (var type in moduleTypes)
            {
                var module = (ICustomModule)Activator.CreateInstance(type)!;
                moduleId = module.ModuleId;
                var info = new ModuleInfo(
                    module.ModuleId,
                    module.DisplayName,
                    module.Version,
                    module.Description,
                    dllPath,
                    IsLoaded: true,
                    LoadError: null
                );
                _loaded.Add((module, info));
                _logger.LogInformation("Loaded plugin: {DisplayName} v{Version} [{ModuleId}]",
                    module.DisplayName, module.Version, module.ModuleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from {File}.", Path.GetFileName(dllPath));
            var failedInfo = new ModuleInfo(
                moduleId ?? Path.GetFileNameWithoutExtension(dllPath),
                Path.GetFileNameWithoutExtension(dllPath),
                "0.0.0",
                string.Empty,
                dllPath,
                IsLoaded: false,
                LoadError: ex.Message
            );
            _loaded.Add((null!, failedInfo));
        }
    }
}
