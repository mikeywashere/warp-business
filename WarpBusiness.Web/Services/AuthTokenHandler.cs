using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace WarpBusiness.Web.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthTokenHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var token = await httpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Forward the selected tenant ID from cookie to the API
            if (httpContext.Request.Cookies.TryGetValue("X-Selected-Tenant", out var tenantId)
                && !string.IsNullOrEmpty(tenantId))
            {
                request.Headers.Add("X-Tenant-Id", tenantId);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
