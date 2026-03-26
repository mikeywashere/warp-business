using WarpBusiness.Plugin.Abstractions;

namespace WarpBusiness.Api.Plugins;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Discovers plugins in the plugins directory, calls ConfigureServices on each,
    /// and registers ModuleRegistry in DI. Also accepts optional first-party modules
    /// that are pre-registered and included in the ModuleRegistry alongside folder-scanned modules.
    /// Call this BEFORE AddControllers().
    /// </summary>
    public static IServiceCollection AddCustomModules(
        this IServiceCollection services,
        IConfiguration configuration,
        string pluginsDirectory,
        IEnumerable<ICustomModule>? firstPartyModules = null)
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var loader = new PluginLoader(loggerFactory.CreateLogger<PluginLoader>(), pluginsDirectory);
        loader.DiscoverAndLoad();

        var loadedModules = new List<ICustomModule>();
        var moduleInfos = new List<ModuleInfo>();

        foreach (var (module, info) in loader.LoadedModules)
        {
            moduleInfos.Add(info);
            if (!info.IsLoaded) continue;

            module.ConfigureServices(services, configuration);
            loadedModules.Add(module);
        }

        if (firstPartyModules is not null)
        {
            foreach (var module in firstPartyModules)
            {
                module.ConfigureServices(services, configuration);
                loadedModules.Add(module);
                moduleInfos.Add(new ModuleInfo(
                    module.ModuleId, module.DisplayName, module.Version,
                    module.Description, "built-in", IsLoaded: true, LoadError: null));
            }
        }

        services.AddSingleton(new ModuleRegistry(loadedModules, moduleInfos));

        return services;
    }

    /// <summary>
    /// Calls Configure on each successfully loaded module.
    /// Call this on the WebApplication AFTER UseRouting().
    /// </summary>
    public static WebApplication UseCustomModules(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<ModuleRegistry>();
        foreach (var module in registry.Modules)
        {
            try
            {
                module.Configure(app);
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<ModuleRegistry>>();
                logger.LogError(ex, "Module {ModuleId} threw during Configure().", module.ModuleId);
            }
        }
        return app;
    }
}
