using System.Net;

namespace WarpBusiness.Api.Tests.Helpers;

/// <summary>
/// A fake HttpMessageHandler for testing KeycloakAdminService without hitting a real Keycloak server.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public void QueueResponse(HttpResponseMessage response) => _responses.Enqueue(response);

    /// <summary>
    /// Queues a successful user creation response with a Location header containing the user ID.
    /// </summary>
    public void QueueCreateUserResponse(string keycloakUserId)
    {
        // First response: token
        QueueTokenResponse();

        // Second response: create user with Location header
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri($"http://localhost/admin/realms/warpbusiness/users/{keycloakUserId}");
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Queues a successful update/delete response.
    /// </summary>
    public void QueueSuccessResponse()
    {
        QueueTokenResponse();
        _responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    public void QueueTokenResponse()
    {
        var tokenResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"access_token":"fake-token","expires_in":300}""",
                System.Text.Encoding.UTF8, "application/json")
        };
        _responses.Enqueue(tokenResponse);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_responses.Count == 0)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        return Task.FromResult(_responses.Dequeue());
    }
}
