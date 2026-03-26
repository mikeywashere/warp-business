# WarpBusiness Plugin Development Guide

## Overview

The WarpBusiness plugin system lets third-party developers extend the CRM by dropping a DLL into the host's `plugins/` directory. On startup, the host scans that folder, loads any assembly containing a class that implements `ICustomModule`, instantiates it, and integrates it into the application.

Plugins can contribute:
- **Services** — registered into the DI container alongside core services
- **API endpoints** — minimal-API routes mapped onto the host's `WebApplication`
- **Blazor pages** — routable `@page` components discovered via assembly registration
- **Navigation items** — sidebar entries injected into the CRM shell

---

## The `ICustomModule` Interface

Located in `WarpBusiness.Plugin.Abstractions`.

```csharp
public interface ICustomModule
{
    string ModuleId     { get; }   // Unique reverse-DNS ID: "com.example.my-ext"
    string DisplayName  { get; }   // Human-readable name
    string Version      { get; }   // Semver string
    string Description  { get; }   // Short description shown on the modules page

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    void Configure(WebApplication app);

    IEnumerable<ModuleNavItem>  GetNavItems();
    IEnumerable<Assembly>       GetBlazorAssemblies();
}
```

### Method contracts

| Method | When called | Purpose |
|--------|-------------|---------|
| `ConfigureServices` | During `builder.Build()` | Register scoped/singleton/transient services, options, DbContexts, etc. |
| `Configure` | After `app` is built, before `app.Run()` | Map minimal-API routes, add middleware, register health checks. |
| `GetNavItems` | At startup; cached | Return sidebar `ModuleNavItem` records the shell renders. |
| `GetBlazorAssemblies` | At startup; passed to Router | Return assemblies with `@page` Razor components. Pass `Assembly.GetExecutingAssembly()` if your plugin has pages; return empty otherwise. |

---

## Creating a Plugin

### 1. Create the project

```powershell
dotnet new classlib -n MyCompany.MyExtension -f net10.0
cd MyCompany.MyExtension
dotnet add reference <path-to>/WarpBusiness.Plugin.Abstractions.csproj
```

If your plugin includes Razor components, change the SDK to `Microsoft.NET.Sdk.Razor` in the `.csproj` and add a `FrameworkReference` to `Microsoft.AspNetCore.App`.

### 2. Implement `ICustomModule`

```csharp
using WarpBusiness.Plugin.Abstractions;

public class MyModule : ICustomModule
{
    public string ModuleId    => "com.mycompany.my-extension";
    public string DisplayName => "My Extension";
    public string Version     => "1.0.0";
    public string Description => "Adds XYZ to WarpBusiness.";

    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<MyFeatureService>();
    }

    public void Configure(WebApplication app)
    {
        app.MapGet("/api/my-extension/status", () => Results.Ok("running"))
           .RequireAuthorization();
    }

    public IEnumerable<ModuleNavItem> GetNavItems() =>
    [
        new("My Feature", "my-feature", Icon: "bi-gear", DisplayOrder: 150)
    ];

    public IEnumerable<Assembly> GetBlazorAssemblies() =>
        [Assembly.GetExecutingAssembly()];
}
```

### 3. Build and deploy

```powershell
dotnet publish -c Release -o ./publish
```

Copy everything from `./publish` into the host's `plugins/` directory (relative to the WarpBusiness API binary). The host loads all DLLs in that directory at startup.

---

## `ModuleNavItem` Record

```csharp
public record ModuleNavItem(
    string Label,
    string Href,
    string? Icon         = null,   // Bootstrap icon class, e.g. "bi-star"
    string? RequiredRole = null,   // null = visible to all authenticated users
    int    DisplayOrder  = 100     // Lower numbers appear first
);
```

---

## Runtime Metadata — `GET /api/modules`

The host exposes an endpoint that returns a list of `ModuleInfo` records:

```json
[
  {
    "moduleId":     "com.mycompany.my-extension",
    "displayName":  "My Extension",
    "version":      "1.0.0",
    "description":  "Adds XYZ to WarpBusiness.",
    "assemblyPath": "/app/plugins/MyCompany.MyExtension.dll",
    "isLoaded":     true,
    "loadError":    null
  }
]
```

If a plugin fails to load, `isLoaded` is `false` and `loadError` contains the exception message. The host continues loading other plugins.

---

## Reference Implementation — `WarpBusiness.Plugin.Sample`

`WarpBusiness.Plugin.Sample` is included in the solution as a working example:

| File | Purpose |
|------|---------|
| `SampleModule.cs` | `ICustomModule` implementation — registers `SampleService`, maps `/api/sample/hello`, contributes a nav item and Blazor assembly |
| `SampleService.cs` | Example singleton service |
| `Pages/SamplePage.razor` | Blazor page at `/sample`, requires authentication |
| `_Imports.razor` | Global `@using` directives for the plugin's Razor components |

Copy this project as a starting point for new plugins.

---

## Limitations

- **Shared process** — plugins run in the same process as the host. A crashing plugin can destabilize the entire application.
- **No hot-reload** — plugins are loaded once at startup. Deploying a new or updated plugin requires a host restart.
- **No sandbox** — plugins have full access to the DI container and request pipeline. Only deploy plugins from trusted sources.
- **Single AppDomain** — there is no isolation between plugin assemblies. Name or version conflicts between plugin dependencies may cause `TypeLoadException`.
- **Auth model inherited** — plugins inherit the host's authentication/authorization setup. Use `RequireAuthorization()` and role policies accordingly.
