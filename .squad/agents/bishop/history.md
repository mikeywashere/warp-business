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

### 2026-03-26: Auth/Infra Code Review Fixes (PR #4)

Fixed three code review findings from Ripley's full review:

**1. ExternalIdentityMapper AuthProvider Assignment**
- New OIDC users now get `AuthProvider = provider.ToString()` (e.g. `"Keycloak"`, `"Microsoft"`) set on provisioning
- Returning users whose `AuthProvider` is `"Local"` or empty are corrected on next OIDC login
- Used `provider.ToString()` which maps the `AuthProviderType` enum directly — matches how `AuthController.GetProvider` and `AdminController` display the provider name

**2. K8s Resource Limits and Liveness Probes**
- Added `resources.requests/limits` to all three deployments (API, Web, Portal) with tiered sizing
- Added `livenessProbe` to web and portal (API already had one) using `path: /` on port 8080
- These are production-safety requirements, not optional

**3. Secrets Template Sanitization**
- Replaced all base64-encoded placeholder values with `REPLACE_WITH_BASE64_ENCODED_VALUE`
- Added `keycloak-admin-password` key that was missing
- Standardized header comment block

**Git Learnings:** In a shared worktree environment with multiple agents active, branch context can silently shift between operations. Always verify branch immediately before commit with `git branch` — do not trust earlier `checkout` output. Use `git add <specific-files>` to scope commits tightly and prevent accidentally including other agents' staged work.



Created and committed the `20260326030154_AddRefreshTokens` migration to persist the RefreshToken entity schema to PostgreSQL.

**Migration Details:**
- **Table:** `RefreshTokens` with 9 columns (Id, UserId, TokenHash, FamilyId, ExpiresAt, CreatedAt, RevokedAt, ReplacedByTokenHash, DeviceHint)
- **Indexes:** Unique index on `TokenHash` (for fast token lookups), composite index on `(UserId, FamilyId)` (for reuse detection queries)
- **Column Types:** UUIDs for Id, varchar with appropriate lengths for identifiers, timestamptz for temporal columns
- **Generated:** Using `dotnet ef migrations add` with DesignTimeDbContextFactory pointing to `warpbusiness_dev` database

**Status:** Migration file committed to repository but not yet applied. Database update will happen during deployment or manual `dotnet ef database update`.

### 2026-03-27: Multi-Tenancy Auth Architecture Analysis

Performed comprehensive analysis of authentication and authorization implications for adding multi-tenancy to Warp Business. Analysis saved to `.squad/decisions/inbox/bishop-tenancy-auth.md`.

**Key Architectural Decisions:**

**User-Tenant Model:** Recommended single-tenant-per-user for MVP (add `TenantId` to `ApplicationUser`), with future path to multi-tenant membership via join table. Store tenant membership in Warp Business DB (not IdP) for maximum control during early stage.

**JWT Claim Design:** Bake `tenant_id` claim into token at login time (not per-request lookup). Use lowercase-with-underscore naming (`tenant_id`, not `tid`) to avoid Azure AD collision. Add `tenant_name` for display purposes.

**OIDC Strategy Progression:**
1. **Phase 1 (MVP):** Single IdP, Shared — one Keycloak/Auth0 realm, tenant as claim. Simplest implementation.
2. **Phase 2 (Subdomains):** Single IdP, Tenant-Aware — per-tenant Keycloak realms, subdomain determines realm. Enables `[tenant].warp-business.com`.
3. **Phase 3 (Enterprise):** Per-Tenant IdP — each tenant brings their own OIDC provider. Complex, reserve for enterprise contracts only.

**Subdomain Auth Flow:** For `[business-name].warp-business.com`, recommend **Shared Callback Domain** pattern: all tenants redirect to `https://auth.warp-business.com/signin-oidc`, then post-login redirect to tenant subdomain with wildcard cookie domain (`.warp-business.com`). Avoids IdP redirect_uri wildcard limitation. Requires wildcard DNS and TLS cert.

**Authorization Model:** Keep global roles (`Admin`, `Manager`, `User`) but enforce tenant filtering at data layer. Admin is admin **within their tenant only**. Add policies: `RequireTenant` (requires `tenant_id` claim), `TenantAdmin` (role + tenant claim). Reject composite roles (`tenant-id:role`) as over-engineered for current needs.

**Critical Security Risks Identified:**
1. **Missing Tenant Filter in Query:** Most common isolation breach. Mitigate with base `TenantScopedService` class and integration tests for cross-tenant access attempts.
2. **Tenant Claim Tampering:** JWT signing key in appsettings.json (already flagged by Ripley). Move to secrets manager, enforce short expiry (already 15 min).
3. **Subdomain Hijacking:** Reserve critical slugs (`admin`, `api`, `www`, `auth`), validate slug format, pre-register DNS for reserved names.

**Defense-in-Depth Recommendations:** PostgreSQL Row-Level Security as last resort, audit logging with `(UserId, TenantId, Action)` tuple, per-tenant rate limiting, tenant isolation test suite.

**Implementation Sequence:** Phase 1 adds `TenantId` to all entities + JWT claims + service filtering (1-2 weeks). Phase 2 adds subdomain routing + wildcard DNS/TLS (2-3 weeks). Phase 3 (optional) adds Keycloak realm-per-tenant (4-6 weeks).

**Current State Observations:**
- No tenant concept exists anywhere in codebase yet
- CRM entities have `CreatedBy`/`OwnerId` (user-scoped), no tenant scope
- Employee entity has zero ownership tracking
- Roles are global (Admin can delete across all data)
- Blazor Server uses HttpOnly cookies — tenant isolation via cookie domain (`.warp-business.com`) is straightforward
- `[Authorize(Roles = "Admin")]` used on AdminController and DELETE endpoints; needs tenant-scoping via data filter

**Tech Stack Fit:** Multi-provider OIDC architecture (already implemented) supports all three tenancy strategies without refactoring. `ExternalIdentityMapper.EnsureUserAsync` is natural injection point for tenant assignment from IdP claims. Refresh token rotation (already implemented) works with tenant-scoped sessions (no changes needed).

### 2026-03-27: Admin User Seeding (ASP.NET Identity + Keycloak)

Implemented idempotent admin user seeding across both identity stores.

**ASP.NET Identity (Program.cs):**
- Seeds `mikenging@hotmail.com` with Admin role on every startup (skips if user exists)
- Password hash set directly via `PasswordHasher.HashPassword()` bypassing validators — "WooHoo" doesn't meet the configured policy (8 chars, digit required) but is intentionally weak as a temporary seed password
- Skipped in `Test` environment (via `app.Environment.IsEnvironment("Test")`) to preserve test isolation — the `DeleteUser_LastAdmin_ReturnsConflict` test depends on exact admin count
- User created with `EmailConfirmed = true` so login works immediately

**Keycloak Realm Import:**
- Created `src/WarpBusiness.AppHost/keycloak/warpbusiness-realm.json` with full realm config
- Realm: `warpbusiness`, Client: `warpbusiness-api` (public, PKCE, direct-access grants)
- Admin user with `temporary: true` credential — Keycloak forces UPDATE_PASSWORD on first login
- Role mapper exposes realm roles in JWT `roles` claim for ExternalIdentityMapper compatibility
- Wired into AppHost via `.WithRealmImport("keycloak")` on the Keycloak resource

**Security Notes:**
- Password "WooHoo" is weak by design — must be changed on first Keycloak login
- ASP.NET Identity side has no built-in "force password change" mechanism; consider adding `MustChangePassword` flag to ApplicationUser if Local provider needs this
- Seed runs in all environments except Test — production bootstrap needs this admin to exist

**Key Files:** `src/WarpBusiness.Api/Program.cs` (lines 117-153), `src/WarpBusiness.AppHost/Program.cs` (line 11), `src/WarpBusiness.AppHost/keycloak/warpbusiness-realm.json`

### 2026-03-27: Multi-Tenancy Auth Layer Implementation

Implemented the full auth layer for multi-tenancy. Key patterns and decisions:

**JWT Claims Design:**
- `GenerateAccessToken(user, roles, tenantId?, tenantSlug?, tenantRole?, allTenantIds?)` — optional tenant params; when tenantId provided, bakes `tenant_id`, `tenant_slug`, `tenant_role`, `tenants[]` into token
- `GeneratePreAuthToken(user, tenantIds[])` — used for multi-tenant login: includes `tenants` list but NO `tenant_id`; forces client to call `/select-tenant`
- `RefreshToken.ActiveTenantId` (nullable Guid) — carries the user's active tenant through token rotation so refresh preserves tenant selection

**Login flow:**
- 0 tenants → basic token
- 1 tenant → full token (auto-resolved)
- 2+ tenants → pre-auth token with `tenants[]` list; user picks via UI, POST /api/auth/select-tenant issues full token

**Claims Transformation (`TenantClaimsTransformation`):**
- Runs on every request via `IClaimsTransformation`
- If `tenant_id` already in token → no-op (token is authoritative)
- If missing but user has 1 active tenant → auto-inject (handles legacy tokens)
- Always injects `tenants[]` list if missing

**Authorization Policies:**
- `RequireActiveTenant` → `RequireClaim("tenant_id")` — applied to all CRM and EmployeeManagement controllers
- `RequireTenantAdmin` → `RequireClaim("tenant_id") + RequireClaim("tenant_role", "TenantAdmin")` — applied to SAML endpoints and future tenant admin endpoints

**Cross-Tenant Guard (`RequireTenantRouteMatchAttribute`):**
- Action filter applied to routes with `{tenantId}` route param
- Validates JWT `tenant_id` matches the URL param
- Returns 403 on mismatch — prevents IDOR at the tenant boundary

**TenantResolutionMiddleware:**
- Extracts subdomain slug from Host header, stores in `HttpContext.Items["TenantSlug"]`
- Enabled via `WarpBusiness:SubdomainRoutingEnabled` config (default false = Phase 2 prep)
- When enabled: validates JWT `tenant_slug` matches subdomain, rejects with 403 on mismatch
- Reserved slugs: `www, api, auth, admin, mail, cdn, static, app, portal`

**SAML:**
- `ITenantSamlService` / `TenantSamlService` — config storage fully implemented (GET, save, enable with validation)
- `TestConnectionAsync` stubbed with TODO for Sustainsys.Saml2 integration
- SAML endpoints use `RequireTenantAdmin` policy + `RequireTenantRouteMatch` filter

**Key observation:** TenantsController and ITokenService already existed with partial tenant support from Hicks. I extended both rather than replacing them. The `TenantClaimsTransformation` was also already in HEAD — confirmed it was properly registered and working.

