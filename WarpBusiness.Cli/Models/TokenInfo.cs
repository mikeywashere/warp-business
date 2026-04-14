namespace WarpBusiness.Cli.Models;

public class TokenInfo
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string KeycloakUrl { get; set; } = string.Empty;
}
