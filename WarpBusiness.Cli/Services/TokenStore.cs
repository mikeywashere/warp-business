using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using WarpBusiness.Cli.Models;

namespace WarpBusiness.Cli.Services;

/// <summary>
/// Stores and retrieves authentication tokens.
/// On Windows, tokens are encrypted with DPAPI (current-user scope).
/// On other platforms, tokens are stored as plain JSON with a warning.
/// </summary>
public static class TokenStore
{
    private static readonly string StorageDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "warp");

    private static readonly string CredentialsPath = Path.Combine(StorageDir, "credentials.dat");

    public static async Task SaveAsync(TokenInfo token)
    {
        Directory.CreateDirectory(StorageDir);

        var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = false });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(CredentialsPath, encrypted);
        }
        else
        {
            // Non-Windows: write plaintext with restricted permissions warning
            Console.WriteLine("⚠️  Token stored without encryption (Windows DPAPI not available).");
            await File.WriteAllBytesAsync(CredentialsPath, bytes);
        }
    }

    public static TokenInfo? Load()
    {
        if (!File.Exists(CredentialsPath))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(CredentialsPath);

            byte[] json;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                json = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                json = bytes;
            }

            return JsonSerializer.Deserialize<TokenInfo>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Delete()
    {
        if (File.Exists(CredentialsPath))
            File.Delete(CredentialsPath);
    }

    public static bool Exists() => File.Exists(CredentialsPath);
}
