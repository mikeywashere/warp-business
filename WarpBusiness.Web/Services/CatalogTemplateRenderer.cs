using HandlebarsDotNet;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Concurrent;

namespace WarpBusiness.Web.Services;

public class CatalogTemplateRenderer
{
    private readonly IWebHostEnvironment _env;
    private readonly ConcurrentDictionary<string, HandlebarsTemplate<object, object>> _cache = new();

    public CatalogTemplateRenderer(IWebHostEnvironment env) => _env = env;

    public string Render(string templateName, object data)
    {
        var template = _cache.GetOrAdd(templateName, name =>
        {
            var path = Path.Combine(_env.WebRootPath, "catalog-templates", $"{name}.hbs");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Catalog template '{name}' not found.", path);
            var source = File.ReadAllText(path);
            return Handlebars.Compile(source);
        });
        return template(data);
    }

    public bool TemplateExists(string templateName)
    {
        var path = Path.Combine(_env.WebRootPath, "catalog-templates", $"{templateName}.hbs");
        return File.Exists(path);
    }

    public IEnumerable<string> GetAvailableTemplates()
    {
        var dir = Path.Combine(_env.WebRootPath, "catalog-templates");
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.hbs")
            .Select(f => Path.GetFileNameWithoutExtension(f));
    }
}
