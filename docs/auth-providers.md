# Auth Provider Configuration

Warp Business supports three authentication providers. The active provider is selected via `appsettings.json` — no code changes required.

## Switching Providers

In `appsettings.json` (or environment variable `AuthProvider__ActiveProvider`):

| Value | Provider | Use case |
|---|---|---|
| `Local` | ASP.NET Core Identity + JWT | Default, self-hosted, dev/test |
| `Keycloak` | Keycloak OIDC | Self-hosted SSO, enterprise |
| `Microsoft` | Microsoft Entra ID | Microsoft 365 orgs |

## Local (Default)

No additional config needed. Uses the `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` settings.

## Keycloak

```json
{
  "AuthProvider": {
    "ActiveProvider": "Keycloak",
    "Keycloak": {
      "Authority": "https://keycloak.example.com/realms/your-realm",
      "ClientId": "warpbusiness-api",
      "Audience": "warpbusiness-api"
    }
  }
}
```

In local dev, Keycloak runs as an Aspire container on port 8080. Use the Keycloak admin console at http://localhost:8080.

## Microsoft Entra ID

```json
{
  "AuthProvider": {
    "ActiveProvider": "Microsoft",
    "Microsoft": {
      "TenantId": "your-tenant-id",
      "ClientId": "your-app-client-id",
      "Audience": "api://your-app-client-id"
    }
  }
}
```

## How External Users Are Provisioned

On first login via Keycloak or Microsoft, the system automatically creates a local `ApplicationUser` record (shadow account) using the OIDC token claims. The user is assigned the `User` role by default. Admins can promote via the Admin panel.
