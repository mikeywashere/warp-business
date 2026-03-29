using System.Net;
using System.Net.Http.Headers;

namespace WarpBusiness.MobileApp.Services;

public class AuthDelegatingHandler : DelegatingHandler
{
    private readonly AuthService _authService;
    private bool _isRefreshing;

    public AuthDelegatingHandler(AuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_authService.CurrentToken != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.CurrentToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !_isRefreshing)
        {
            _isRefreshing = true;
            try
            {
                var refreshResult = await _authService.RefreshTokenAsync();
                if (refreshResult != null)
                {
                    var retryRequest = await CloneRequestAsync(request);
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.CurrentToken);
                    response = await base.SendAsync(retryRequest, cancellationToken);
                }
            }
            finally { _isRefreshing = false; }
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content != null)
        {
            var content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);
            if (request.Content.Headers.ContentType != null)
                clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
        }
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
