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

### 2026-03-25: Refresh Token Strategy with Rotation and Reuse Detection

Implemented secure refresh token infrastructure for Local JWT provider with defense-in-depth approach to token lifecycle management.

**Architecture:**
- **RefreshToken Entity**: Opaque tokens (256-bit random hex), stored hashed (SHA-256), bound to user + device family
- **Token Rotation**: Every refresh operation issues a new token and invalidates the old one, preventing replay attacks
- **Family Tracking**: Each token belongs to a family (identified by FamilyId). When rotated, new token inherits family ID
- **Reuse Attack Detection**: If a revoked token (marked with `ReplacedByTokenHash`) is used, entire family is revoked — indicates theft or compromise
- **HttpOnly Cookies**: Refresh tokens delivered as `warp_refresh` cookie (HttpOnly, Secure, SameSite=Strict) to prevent XSS access

**Token Expiry:**
- Access tokens: 15 minutes (production), 60 minutes (development)
- Refresh tokens: 7 days (production), 30 days (development)
- Configurable via `Jwt:AccessTokenExpiryMinutes` and `Jwt:RefreshTokenExpiryDays`

**Endpoints:**
- `POST /api/auth/refresh`: Validates refresh token, rotates it, returns new access token
- `POST /api/auth/logout`: Revokes all user refresh tokens, deletes cookie
- Login and registration endpoints now issue both access + refresh tokens

**Database Schema:**
- RefreshTokenConfiguration applies via `ApplyConfigurationsFromAssembly`
- Unique index on `TokenHash`, composite index on `(UserId, FamilyId)` for fast lookups
- Tracks: TokenHash, FamilyId, ExpiresAt, CreatedAt, RevokedAt, ReplacedByTokenHash, DeviceHint

**Security Properties:**
- Never stores raw tokens (only SHA-256 hash)
- Sliding window: only one active token per device family at a time
- Automatic family revocation on suspicious activity (token reuse after rotation)
- Device hint (User-Agent) stored for forensics/audit trail

**Frontend Integration:** Vasquez notified to implement 401 → refresh → retry flow in WarpApiClient.cs. Refresh is cookie-based, no manual token management needed on client.

