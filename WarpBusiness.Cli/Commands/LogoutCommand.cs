using System.CommandLine;
using WarpBusiness.Cli.Services;

namespace WarpBusiness.Cli.Commands;

public static class LogoutCommand
{
    public static Command Create()
    {
        var cmd = new Command("logout", "Remove stored login credentials");
        cmd.SetHandler(Handle);
        return cmd;
    }

    private static void Handle()
    {
        if (!TokenStore.Exists())
        {
            Console.WriteLine("Not logged in.");
            return;
        }

        TokenStore.Delete();
        Console.WriteLine("✅ Logged out. Credentials removed.");
    }
}
