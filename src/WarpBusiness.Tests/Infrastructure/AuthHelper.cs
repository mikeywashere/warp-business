using System.Net.Http.Json;
using WarpBusiness.Shared.Auth;

namespace WarpBusiness.Tests.Infrastructure;

public static class AuthHelper
{
    public static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string email = "test@example.com",
        string password = "Test1234!",
        string firstName = "Test",
        string lastName = "User")
    {
        var response = await client.PostAsJsonAsync("api/auth/register",
            new RegisterRequest(email, password, firstName, lastName));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
