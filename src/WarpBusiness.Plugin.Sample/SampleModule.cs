using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WarpBusiness.Plugin.Abstractions;

namespace WarpBusiness.Plugin.Sample;

public class SampleModule : ICustomModule
{
    public string ModuleId => "com.warpbusiness.sample-plugin";
    public string DisplayName => "Sample Plugin";
    public string Version => "1.0.0";
    public string Description => "Demonstrates the WarpBusiness plugin system. Replace with your own module.";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register sample services here
        services.AddSingleton<SampleService>();
    }

    public void Configure(WebApplication app)
    {
        // Register minimal API endpoints or middleware here
        app.MapGet("/api/sample/hello", () => new { Message = "Hello from Sample Plugin!", ModuleId })
           .RequireAuthorization();
    }

    public IEnumerable<ModuleNavItem> GetNavItems() =>
    [
        new ModuleNavItem(
            Label: "Sample Plugin",
            Href: "sample",
            Icon: "bi-puzzle",
            DisplayOrder: 200
        )
    ];

    public IEnumerable<Assembly> GetBlazorAssemblies() =>
        [Assembly.GetExecutingAssembly()];
}
