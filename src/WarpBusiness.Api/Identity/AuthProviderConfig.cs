namespace WarpBusiness.Api.Identity;

/// <summary>
/// Controls which authentication provider is active.
/// Set via appsettings.json AuthProvider:ActiveProvider.
/// </summary>
public enum AuthProviderType
{
    /// <summary>Local ASP.NET Core Identity + JWT (default)</summary>
    Local,
    /// <summary>Keycloak OIDC</summary>
    Keycloak,
    /// <summary>Microsoft Entra ID (Azure AD)</summary>
    Microsoft
}

public class AuthProviderOptions
{
    public const string SectionName = "AuthProvider";

    /// <summary>Which provider is active. Default: Local.</summary>
    public AuthProviderType ActiveProvider { get; set; } = AuthProviderType.Local;

    public KeycloakOptions Keycloak { get; set; } = new();
    public MicrosoftOptions Microsoft { get; set; } = new();
}

public class KeycloakOptions
{
    /// <summary>e.g. https://keycloak.example.com/realms/warpbusiness</summary>
    public string Authority { get; set; } = string.Empty;
    /// <summary>The client ID registered in Keycloak</summary>
    public string ClientId { get; set; } = string.Empty;
    /// <summary>Keycloak audience claim value (usually the client ID)</summary>
    public string Audience { get; set; } = string.Empty;
    /// <summary>Claim name that carries the user's email in Keycloak tokens</summary>
    public string EmailClaim { get; set; } = "email";
    /// <summary>Claim name for the user's display name</summary>
    public string NameClaim { get; set; } = "name";
}

public class MicrosoftOptions
{
    /// <summary>Azure AD tenant ID or "common" for multi-tenant</summary>
    public string TenantId { get; set; } = string.Empty;
    /// <summary>Azure AD app (client) ID</summary>
    public string ClientId { get; set; } = string.Empty;
    /// <summary>The audience to validate — usually the ClientId or api:// URI</summary>
    public string Audience { get; set; } = string.Empty;
}
