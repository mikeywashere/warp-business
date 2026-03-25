# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** Warp Business — Business Management System (CRM first)
- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core, Auth/Authz
- **Role:** Auth Specialist — authentication, authorization, identity, security
- **Created:** 2026-03-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-25: Multi-Provider OIDC Architecture

Implemented a config-driven multi-provider authentication system that supports Local (ASP.NET Core Identity + JWT), Keycloak OIDC, and Microsoft Entra ID.

**Key Design Patterns:**
- **AuthProviderExtensions**: Extension method pattern that switches authentication middleware based on `appsettings.json` `AuthProvider:ActiveProvider` value. Each provider (Local/Keycloak/Microsoft) has its own private configuration method.
- **ExternalIdentityMapper**: Service that auto-provisions shadow `ApplicationUser` records on first OIDC login by mapping external claims (email, given_name, family_name) to local Identity store. Handles claim name variations across providers (e.g., `ClaimTypes.Email` vs `"email"` vs `"preferred_username"`).
- **JWT Bearer OnTokenValidated Event**: Hook point for calling ExternalIdentityMapper after token validation. Keycloak uses direct JWT Bearer events; Microsoft uses PostConfigure to preserve Microsoft.Identity.Web's built-in event chain.
- **Provider Discovery Endpoint**: `GET /api/auth/provider` returns active provider info so clients can adapt their auth flow (e.g., redirect to Keycloak vs show local login form).

**Config Structure:** AuthProviderOptions with nested KeycloakOptions/MicrosoftOptions. All provider details live in `appsettings.json`, not hardcoded. Swap providers by changing one config value.

**Testing/Dev:** Added Aspire.Hosting.Keycloak to AppHost for local Keycloak container. Microsoft provider requires external Azure AD tenant setup (not containerizable).
