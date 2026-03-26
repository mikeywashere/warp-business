using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace WarpBusiness.Plugin.Abstractions;

/// <summary>
/// Implement this interface in a class library and drop the DLL into the host's
/// plugins/ directory to have it discovered and activated at startup.
/// </summary>
public interface ICustomModule
{
    /// <summary>Unique reverse-DNS style ID, e.g. "com.example.my-extension"</summary>
    string ModuleId { get; }

    string DisplayName { get; }
    string Version { get; }
    string Description { get; }

    /// <summary>Called during host startup to register the module's services.</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Called after the WebApplication is built to configure middleware/endpoints.</summary>
    void Configure(WebApplication app);

    /// <summary>Navigation items to inject into the CRM sidebar.</summary>
    IEnumerable<ModuleNavItem> GetNavItems();

    /// <summary>
    /// Blazor assemblies containing routable @page components.
    /// The host adds these to the Blazor Router's AdditionalAssemblies.
    /// Return empty if no Blazor pages are contributed.
    /// </summary>
    IEnumerable<Assembly> GetBlazorAssemblies();
}
