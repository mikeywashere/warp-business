using System.CommandLine;
using WarpBusiness.Cli.Commands;
using WarpBusiness.Cli.Services;

var root = new RootCommand("warp — WarpBusiness developer CLI");

root.AddCommand(LoginCommand.Create());
root.AddCommand(LogoutCommand.Create());
root.AddCommand(ClearCommand.Create());
root.AddCommand(E2eCommand.Create());

// Show current login status when no subcommand is given
root.SetHandler(() =>
{
    var token = TokenStore.Load();
    if (token is null)
    {
        Console.WriteLine("Not logged in. Run 'warp login' to authenticate.");
    }
    else if (token.ExpiresAt <= DateTimeOffset.UtcNow)
    {
        Console.WriteLine($"Session expired for {token.UserName}. Run 'warp login' to re-authenticate.");
    }
    else
    {
        Console.WriteLine($"Logged in as: {token.UserName}");
        Console.WriteLine($"Token valid until: {token.ExpiresAt:u}");
        Console.WriteLine($"API: {token.ApiUrl}");
    }
});

return await root.InvokeAsync(args);
