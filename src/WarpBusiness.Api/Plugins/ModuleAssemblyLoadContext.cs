using System.Reflection;
using System.Runtime.Loader;

namespace WarpBusiness.Api.Plugins;

/// <summary>
/// Loads a plugin assembly in an isolated context.
/// Falls back to the default context for shared framework types (ASP.NET Core, etc.)
/// so that ICustomModule references are the same type on both sides.
/// </summary>
internal sealed class ModuleAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public ModuleAssemblyLoadContext(string assemblyPath) : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(assemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Let the default context handle shared framework assemblies.
        // This ensures ICustomModule from the plugin and from the host are the same CLR type.
        var defaultAssembly = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (defaultAssembly is not null)
            return null; // null = defer to default context

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
