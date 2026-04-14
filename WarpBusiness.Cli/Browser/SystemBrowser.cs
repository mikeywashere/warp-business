using System.Net;
using System.Text;
using IdentityModel.OidcClient.Browser;

namespace WarpBusiness.Cli.Browser;

/// <summary>
/// Opens the system browser for OAuth PKCE login and listens on a local HTTP port for the callback.
/// </summary>
public class SystemBrowser : IBrowser
{
    private readonly int _port;

    public SystemBrowser(int port)
    {
        _port = port;
    }

    public string RedirectUri => $"http://127.0.0.1:{_port}/callback";

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options,
        CancellationToken cancellationToken = default)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        listener.Start();

        OpenDefaultBrowser(options.StartUrl);

        try
        {
            var getContext = listener.GetContextAsync();
            var timeout = Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            var completed = await Task.WhenAny(getContext, timeout);

            if (completed == timeout)
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.Timeout,
                    Error = "Login timed out after 5 minutes."
                };
            }

            var context = await getContext;
            var callbackUrl = context.Request.Url?.AbsoluteUri ?? string.Empty;

            var html = "<html><body style='font-family:sans-serif;text-align:center;padding:40px'>" +
                       "<h2>✅ Login successful!</h2>" +
                       "<p>You may close this tab and return to the terminal.</p>" +
                       "</body></html>";
            var responseBytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
            context.Response.Close();

            return new BrowserResult
            {
                Response = callbackUrl,
                ResultType = BrowserResultType.Success
            };
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void OpenDefaultBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            Console.WriteLine($"Could not open browser automatically. Navigate to:\n{url}");
        }
    }

    public static int GetRandomUnusedPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
