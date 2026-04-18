using System.Text;
using System.Text.Json;

namespace AseToolkit;

// ─────────────────────────────────────────────────────────────
//  AseExporter — CSV, JSON, and console output utilities
// ─────────────────────────────────────────────────────────────

public static class AseExporter
{
    /// <summary>
    /// Exports all colors to a CSV file with columns:
    /// Group, Name, Model, R, G, B, Hex, ColorType
    /// </summary>
    public static void ToCsv(List<AseColor> colors, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Group,Name,Model,R,G,B,Hex,ColorType");

        foreach (var c in colors)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(c.Group),
                EscapeCsv(c.Name),
                c.Model.Trim(),
                c.R, c.G, c.B,
                c.Hex,
                c.ColorType));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Exports all colors to a JSON array file.
    /// </summary>
    public static void ToJson(List<AseColor> colors, string path)
    {
        var export = colors.Select(c => new
        {
            group = c.Group,
            name = c.Name,
            model = c.Model.Trim(),
            r = c.R,
            g = c.G,
            b = c.B,
            hex = c.Hex,
            colorType = c.ColorType
        });

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        File.WriteAllText(path,
            JsonSerializer.Serialize(export, options),
            Encoding.UTF8);
    }

    /// <summary>
    /// Prints summary statistics to the console.
    /// </summary>
    public static void PrintStats(List<AseColor> colors)
    {
        Console.WriteLine($"Total colors: {colors.Count}");
        Console.WriteLine();

        // By color model
        Console.WriteLine("By color model:");
        foreach (var group in colors.GroupBy(c => c.Model.Trim())
                                    .OrderByDescending(g => g.Count()))
            Console.WriteLine(
                $"  {group.Key,-6} {group.Count(),5} " +
                $"({100.0 * group.Count() / colors.Count:F1}%)");
        Console.WriteLine();

        // By group
        var groups = colors.Where(c => !string.IsNullOrEmpty(c.Group))
                           .GroupBy(c => c.Group)
                           .OrderByDescending(g => g.Count())
                           .ToList();

        if (groups.Any())
        {
            Console.WriteLine("By group:");
            foreach (var g in groups)
                Console.WriteLine($"  {g.Key,-30} {g.Count(),5}");

            var ungrouped = colors.Count(c => string.IsNullOrEmpty(c.Group));
            if (ungrouped > 0)
                Console.WriteLine($"  {"(ungrouped)",-30} {ungrouped,5}");
            Console.WriteLine();
        }

        // By color type
        Console.WriteLine("By color type:");
        foreach (var ct in colors.GroupBy(c => c.ColorType).OrderBy(g => g.Key))
        {
            string label = ct.Key switch
            {
                0 => "Global",
                1 => "Spot",
                2 => "Normal",
                _ => $"Unknown({ct.Key})"
            };
            Console.WriteLine($"  {label,-10} {ct.Count(),5}");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
