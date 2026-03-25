using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Api.Identity;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Auth;

public class AuthControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsTokenAndUserInfo()
    {
        // Arrange
        var request = new RegisterRequest(
            "newuser@example.com", "Test1234!", "Jane", "Doe");

        // Act
        var response = await _client.PostAsJsonAsync("api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrEmpty();
        auth.Email.Should().Be("newuser@example.com");
        auth.FullName.Should().Be("Jane Doe");
        auth.Roles.Should().Contain("User");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest(
            "duplicate@example.com", "Test1234!", "First", "User");
        await _client.PostAsJsonAsync("api/auth/register", request);

        // Act — register again with the same email
        var response = await _client.PostAsJsonAsync("api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var email = "logintest@example.com";
        await _client.PostAsJsonAsync("api/auth/register",
            new RegisterRequest(email, "Test1234!", "Login", "Test"));

        // Act
        var response = await _client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, "Test1234!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var email = "wrongpass@example.com";
        await _client.PostAsJsonAsync("api/auth/register",
            new RegisterRequest(email, "Test1234!", "Wrong", "Pass"));

        // Act
        var response = await _client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, "WrongPassword!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync("api/auth/login",
            new LoginRequest("nobody@example.com", "Test1234!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        // Arrange — use a dedicated client so auth state doesn't bleed into other tests
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(
            client, "metest@example.com");
        client.SetBearerToken(token);

        // Act
        var response = await client.GetAsync("api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.Email.Should().Be("metest@example.com");
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        // Use a fresh client with no auth headers set
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProvider_ReturnsActiveProvider()
    {
        // Act
        var response = await _client.GetAsync("api/auth/provider");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var provider = await response.Content.ReadFromJsonAsync<AuthProviderInfo>();
        provider.Should().NotBeNull();
        provider!.ActiveProvider.Should().Be("Local");
        provider.SupportsLocalLogin.Should().BeTrue();
    }
}
