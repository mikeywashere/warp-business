using NUnit.Framework;
using WarpBusiness.Tests.E2E.Pages;

namespace WarpBusiness.Tests.E2E.Tests;

[TestFixture]
[Category("E2E")]
public class ContactsTests : PageTestBase
{
    [SetUp]
    public async Task AuthenticateAsync()
    {
        await AuthHelper.LoginAsync(Page, BaseUrl, TestEmail, TestPassword);
    }

    [Test]
    public async Task ContactsList_WhenAuthenticated_ShowsTable()
    {
        await RequireAppAsync();

        var contacts = new ContactsPage(Page, BaseUrl);
        await contacts.GoToAsync();
        await contacts.WaitForTableAsync();

        Assert.That(await contacts.IsTableVisibleAsync(), Is.True,
            "Contacts table should be visible when authenticated.");
    }

    [Test]
    public async Task ContactsList_WhenAuthenticated_ShowsCorrectHeading()
    {
        await RequireAppAsync();

        var contacts = new ContactsPage(Page, BaseUrl);
        await contacts.GoToAsync();
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        var heading = await contacts.GetHeadingTextAsync();
        Assert.That(heading, Is.EqualTo("Contacts"),
            "Page heading should be 'Contacts'.");
    }

    [Test]
    public async Task ContactsList_WhenAuthenticated_ShowsExpectedColumns()
    {
        await RequireAppAsync();

        var contacts = new ContactsPage(Page, BaseUrl);
        await contacts.GoToAsync();
        await contacts.WaitForTableAsync();

        var headers = await contacts.GetColumnHeadersAsync();
        Assert.That(headers, Does.Contain("Name"), "Table should have a Name column.");
        Assert.That(headers, Does.Contain("Email"), "Table should have an Email column.");
        Assert.That(headers, Does.Contain("Status"), "Table should have a Status column.");
    }

    [Test]
    public async Task ContactsList_WhenAuthenticated_HasNewContactButton()
    {
        await RequireAppAsync();

        var contacts = new ContactsPage(Page, BaseUrl);
        await contacts.GoToAsync();
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        Assert.That(await contacts.HasNewContactButtonAsync(), Is.True,
            "New Contact button should be visible.");
    }

    [Test]
    public async Task ContactDetail_WhenContactExists_NavigatesToDetailPage()
    {
        await RequireAppAsync();

        var contacts = new ContactsPage(Page, BaseUrl);
        await contacts.GoToAsync();
        await contacts.WaitForTableAsync();

        var rowCount = await contacts.GetRowCountAsync();
        if (rowCount == 0)
            Assert.Ignore("No contacts in the database — skip detail navigation test.");

        await contacts.ClickFirstContactLinkAsync();
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        Assert.That(Page.Url, Does.Contain("/contacts/"),
            "Clicking a contact should navigate to its detail page.");
    }
}
