namespace WarpBusiness.Api.Middleware;

/// <summary>
/// Extracts the tenant slug from the HTTP Host header (e.g. "acme-corp.warp-business.com")
/// and stores it in HttpContext.Items["TenantSlug"].
///
/// When SubdomainRoutingEnabled = true (Phase 2), also validates that the extracted slug
/// matches the JWT's tenant_slug claim, rejecting mismatches with 403.
///
/// Default: SubdomainRoutingEnabled = false — slug extraction only, no enforcement.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _rootDomain;
    private readonly bool _subdomainRoutingEnabled;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
        { "www", "api", "auth", "admin", "mail", "cdn", "static", "app", "portal" };

    public TenantResolutionMiddleware(
        RequestDelegate next,
        IConfiguration config,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _rootDomain = config["WarpBusiness:RootDomain"] ?? "warp-business.com";
        _subdomainRoutingEnabled = config.GetValue<bool>("WarpBusiness:SubdomainRoutingEnabled");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var tenantSlug = ExtractTenantSlug(host);

        if (!string.IsNullOrEmpty(tenantSlug))
        {
            context.Items["TenantSlug"] = tenantSlug;

            if (_subdomainRoutingEnabled)
            {
                var jwtSlug = context.User.FindFirst("tenant_slug")?.Value;
                if (!string.IsNullOrEmpty(jwtSlug) &&
                    !string.Equals(jwtSlug, tenantSlug, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Subdomain tenant slug {Subdomain} does not match JWT tenant_slug {JwtSlug} for {Method} {Path}",
                        tenantSlug, jwtSlug, context.Request.Method, context.Request.Path);

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Tenant mismatch: subdomain and token do not agree" });
                    return;
                }
            }
        }

        await _next(context);
    }

    private string? ExtractTenantSlug(string host)
    {
        if (string.IsNullOrEmpty(host))
            return null;

        if (!host.EndsWith(_rootDomain, StringComparison.OrdinalIgnoreCase))
            return null;

        // Strip root domain and trailing dot
        var prefix = host[..^_rootDomain.Length].TrimEnd('.');
        if (string.IsNullOrEmpty(prefix))
            return null;

        // Reject reserved subdomains to prevent slug hijacking
        if (ReservedSlugs.Contains(prefix))
            return null;

        return prefix;
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
