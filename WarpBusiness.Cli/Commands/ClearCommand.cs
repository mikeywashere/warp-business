using System.CommandLine;
using System.Text.Json;
using WarpBusiness.Cli.Services;

namespace WarpBusiness.Cli.Commands;

public static class ClearCommand
{
    private static readonly Option<bool> YesOption = new(
        "--yes",
        "Skip confirmation prompt");

    public static Command Create()
    {
        var cmd = new Command("clear", "Backup then clear all WarpBusiness data (requires SystemAdministrator role)");
        cmd.AddOption(YesOption);
        cmd.SetHandler(HandleAsync, YesOption);
        return cmd;
    }

    private static async Task HandleAsync(bool yes)
    {
        var token = TokenStore.Load();
        if (token is null)
        {
            Console.Error.WriteLine("❌ Not logged in. Run 'warp login' first.");
            return;
        }

        if (token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Console.Error.WriteLine("❌ Session expired. Run 'warp login' to re-authenticate.");
            return;
        }

        if (!yes)
        {
            Console.Write("⚠️  This will delete ALL data from the WarpBusiness database.\n" +
                          "   A backup will be saved first. Type 'yes' to confirm: ");
            var input = Console.ReadLine();
            if (!string.Equals(input?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled.");
                return;
            }
        }

        var apiClient = new WarpApiClient(token.ApiUrl, token.AccessToken);

        // Step 1: Export backup
        Console.Write("📦 Exporting backup... ");
        string exportJson;
        try
        {
            exportJson = await apiClient.ExportAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n❌ Export failed: {ex.Message}");
            return;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
        var backupFile = Path.Combine(Directory.GetCurrentDirectory(), $"warp-backup-{timestamp}.json");

        // Pretty-print the JSON before saving
        try
        {
            var parsed = JsonDocument.Parse(exportJson);
            exportJson = JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { /* keep raw if parse fails */ }

        await File.WriteAllTextAsync(backupFile, exportJson);
        Console.WriteLine($"done\n   Saved to: {backupFile}");

        // Step 2: Clear data
        Console.Write("🗑️  Clearing database... ");
        try
        {
            var result = await apiClient.ClearAsync();
            Console.WriteLine("done");
            Console.WriteLine($"\n✅ {result.Message}");

            if (result.RowsCleared is { } rows)
            {
                foreach (var (table, count) in rows)
                    Console.WriteLine($"   {table}: {count} rows cleared");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n❌ Clear failed: {ex.Message}");
            Console.WriteLine($"   Your backup is safe at: {backupFile}");
        }
    }
}
