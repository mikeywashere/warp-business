using WarpBusiness.Plugin.Abstractions;

namespace WarpBusiness.Api.Plugins;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Discovers plugins in the plugins directory, calls ConfigureServices on each,
    /// and registers ModuleRegistry in DI.
    /// Call this BEFORE AddControllers().
    /// Note: Controller-based plugins require a custom ApplicationPart registration step.
    /// Prefer Minimal API endpoints registered in Configure() as the primary plugin contribution point.
    /// </summary>
    public static IServiceCollection AddCustomModules(
        this IServiceCollection services,
        IConfiguration configuration,
        string pluginsDirectory)
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
