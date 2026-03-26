namespace WarpBusiness.Plugin.Abstractions;

public class ModuleLoadException(string moduleId, string message, Exception? inner = null)
    : Exception($"Failed to load module '{moduleId}': {message}", inner)
{
    public string ModuleId { get; } = moduleId;
}
