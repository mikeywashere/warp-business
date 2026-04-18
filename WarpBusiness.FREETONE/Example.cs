namespace AseToolkit;

// ─────────────────────────────────────────────────────────────
//  Example usage (remove or replace for your project)
// ─────────────────────────────────────────────────────────────

#if DEBUG

public class Example
{
    public static void ExampleMain(string[] args)
    {
        var filePath = args.Length > 0 ? args[0] : "freetone.ase";

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"File not found: {filePath}");
            Console.Error.WriteLine("Usage: AseParser <path-to-file.ase>");
            return;
        }

        // Parse
        var colors = AseParser.Parse(filePath);

        // Stats
        AseExporter.PrintStats(colors);
        Console.WriteLine();

        // Preview first 20 colors
        Console.WriteLine("── First 20 colors ─────────────────────────────────────");
        foreach (var c in colors.Take(20))
            Console.WriteLine($"  {c.Name,-35} {c.Model} → {c.Hex}");

        if (colors.Count > 20)
            Console.WriteLine($"  ... and {colors.Count - 20} more");

        Console.WriteLine();

        // Export
        var csvPath = Path.ChangeExtension(filePath, ".csv");
        var jsonPath = Path.ChangeExtension(filePath, ".json");

        AseExporter.ToCsv(colors, csvPath);
        Console.WriteLine($"Exported CSV:  {csvPath}");

        AseExporter.ToJson(colors, jsonPath);
        Console.WriteLine($"Exported JSON: {jsonPath}");
    }
}

#endif