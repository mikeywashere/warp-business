using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Auth;

public class RefreshTokenTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public RefreshTokenTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refresh_AfterLogin_ReturnsNewAccessToken()
    {
        // Arrange — create a fresh client with HTTPS to support secure cookies
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var email = "refresh-test@example.com";
        
        await client.PostAsJsonAsync("api/auth/register",
            new RegisterRequest(email, "Test1234!", "Refresh", "Test"));
        
        await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, "Test1234!"));

        // Act — the cookie is set from login, refresh should use it
        var response = await client.PostAsync("api/auth/refresh", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrEmpty();
        auth.Email.Should().Be(email);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        // Arrange — fresh client with no prior login
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("api/auth/refresh", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_AfterLogout_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var email = "logout-refresh@example.com";
        
        var registerResponse = await client.PostAsJsonAsync("api/auth/register",
            new RegisterRequest(email, "Test1234!", "Logout", "Test"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);

        await client.PostAsync("api/auth/logout", null);

        // Act — refresh after logout should fail
        var response = await client.PostAsync("api/auth/refresh", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithValidToken_ReturnsNoContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(client, "logout-valid@example.com");
        client.SetBearerToken(token);

        // Act
        var response = await client.PostAsync("api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange — fresh client with no token
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ContactsMe_WithMatchingContact_ReturnsContact()
    {
        // Arrange
        var client = _factory.CreateClient();
        var email = "contact-me@example.com";
        
        var token = await AuthHelper.RegisterAndGetTokenAsync(client, email);
        client.SetBearerToken(token);

        // Create a contact with the same email
        var createRequest = new CreateContactRequest(
            FirstName: "Contact",
            LastName: "Me",
            Email: email,
            Phone: null,
            JobTitle: null,
            CompanyId: null);
        
        await client.PostAsJsonAsync("api/contacts", createRequest);

        // Act
        var response = await client.GetAsync("api/contacts/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contact = await response.Content.ReadFromJsonAsync<ContactDto>();
        contact.Should().NotBeNull();
        contact!.Email.Should().Be(email);
        contact.FirstName.Should().Be("Contact");
        contact.LastName.Should().Be("Me");
    }

    [Fact]
    public async Task ContactsMe_WithNoMatchingContact_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(client, "no-contact@example.com");
        client.SetBearerToken(token);

        // Act — no contact created for this email
        var response = await client.GetAsync("api/contacts/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ContactsMe_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange — fresh client with no auth
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("api/contacts/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
