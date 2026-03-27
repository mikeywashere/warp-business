using Microsoft.Playwright;
using NUnit.Framework;

namespace WarpBusiness.Tests.E2E;

[SetUpFixture]
public class PlaywrightSetup
{
    [OneTimeSetUp]
    public void InstallPlaywright()
    {
        var exitCode = Program.Main(["install"]);
        if (exitCode != 0)
            throw new InvalidOperationException($"Playwright install failed with exit code {exitCode}.");
    }
}
