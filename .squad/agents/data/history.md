# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, Entity Framework Core, PostgreSQL
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Automatic Token Refresh (2026-04-11)

- **Root cause:** OIDC `SaveTokens = true` stores the access token in the auth cookie at login time. The token is never refreshed automatically — after expiry (typically hours later), every API call returns 401.
- **Two-layer fix:** (1) **Proactive** — `AuthenticatedComponentBase` decodes the JWT `exp` claim (no package needed, just base64url decode the payload) and calls `TokenRefreshService` if within 60 seconds of expiry during the SSR capture phase. (2) **Reactive** — `AuthTokenHandler` catches 401 with `WWW-Authenticate: Bearer error="invalid_token"`, refreshes, and retries the original request with the new token.
- **Public client refresh:** Keycloak `warpbusiness-web` is a public client (no secret). Token endpoint: `POST {keycloakUrl}/realms/warpbusiness/protocol/openid-connect/token` with form body `grant_type=refresh_token&client_id=warpbusiness-web&refresh_token={token}`.
- **Named HttpClient:** Register a dedicated `"keycloak-token"` `HttpClient` for refresh calls — never reuse the API client pipeline (would cause circular dependency through `AuthTokenHandler`).
- **Cookie update:** After refresh in SSR phase, call `httpContext.AuthenticateAsync(Cookies)`, update properties with `UpdateTokenValue`, then `httpContext.SignInAsync`. In circuit phase, update `TokenProvider` in memory only (can't write cookies over SignalR).
- **Request retry:** Call `request.Content.LoadIntoBufferAsync()` before the first send so the content can be re-read for the retry. For our API clients (all use `JsonContent.Create()`), content is replayable anyway.
- **RefreshToken in TokenProvider:** `TokenProvider` now carries both `AccessToken` and `RefreshToken`. `TokenCircuitHandler` captures both at circuit open. `AuthenticatedComponentBase` persists both via `PersistentComponentState` for SSR→circuit transfer.
- **Key files:**
  - `WarpBusiness.Web/Services/TokenRefreshService.cs` — new service (transient DI)
  - `WarpBusiness.Web/Services/TokenProvider.cs` — added `RefreshToken` property
  - `WarpBusiness.Web/Services/AuthTokenHandler.cs` — reactive 401 refresh + retry
  - `WarpBusiness.Web/Services/TokenCircuitHandler.cs` — captures refresh_token on circuit open
  - `WarpBusiness.Web/Components/AuthenticatedComponentBase.cs` — proactive refresh + persists refresh_token
  - `WarpBusiness.Web/Program.cs` — registers `TokenRefreshService`, `"keycloak-token"` HttpClient


### Keycloak Authentication (2026-04-11)

- **Aspire Keycloak packages** are preview-only at 13.2.2: use version `13.2.2-preview.1.26207.2` for both `Aspire.Hosting.Keycloak` (AppHost) and `Aspire.Keycloak.Authentication` (API).
- **AppHost wiring:** `builder.AddKeycloak("keycloak", 8080)` with `.WithDataVolume()` and `.WithRealmImport("./KeycloakConfiguration")`. Port 8080 is pinned for stable OIDC cookie behavior.
- **Realm import:** `WarpBusiness.AppHost/KeycloakConfiguration/warpbusiness-realm.json` — realm `warpbusiness`, clients `warpbusiness-web` (public/OIDC) and `warpbusiness-api` (bearer-only).
- **API auth:** `AddKeycloakJwtBearer("keycloak", realm: "warpbusiness")` with audience `warpbusiness-api`. Weatherforecast endpoint protected with `.RequireAuthorization()`.
- **Keycloak reference** is passed to both API and Web projects in AppHost.cs.

### User Management Backend (2026-04-11)

- **Hybrid auth model:** Keycloak handles authentication (login/passwords/OIDC), our PostgreSQL DB stores application user profiles with roles. API manages both.
- **EF Core setup:** `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.2.2 + `Microsoft.EntityFrameworkCore.Design` 10.0.5. DbContext registered via `builder.AddNpgsqlDbContext<WarpBusinessDbContext>("warpdb")`.
- **Key files:**
  - `WarpBusiness.Api/Models/ApplicationUser.cs` — user entity with KeycloakSubjectId linkage
  - `WarpBusiness.Api/Models/UserRole.cs` — User (0) and SystemAdministrator (1) enum
  - `WarpBusiness.Api/Models/UserDtos.cs` — CreateUserRequest, UpdateUserRequest, UserResponse records
  - `WarpBusiness.Api/Data/WarpBusinessDbContext.cs` — EF Core context with Users DbSet
  - `WarpBusiness.Api/Data/DbInitializer.cs` — IHostedService that runs migrations + seeds admin user
  - `WarpBusiness.Api/Data/Migrations/` — EF Core migrations
  - `WarpBusiness.Api/Services/KeycloakAdminService.cs` — Keycloak Admin REST API client (CRUD users, token management)
  - `WarpBusiness.Api/Endpoints/UserEndpoints.cs` — Minimal API endpoints for user management
- **Authorization:** "SystemAdministrator" policy checks both Keycloak realm_access roles and DB-backed app_role claim. Role enrichment middleware in Program.cs bridges DB roles → claims.
- **Keycloak admin access:** Uses `services:keycloak:http:0` from Aspire service discovery. Admin credentials via `Keycloak:AdminUser` / `Keycloak:AdminPassword` config keys.
- **AppHost changes:** API now has `WaitFor(postgres)` and `WaitFor(keycloak)` for startup ordering. Admin user env var passed.
- **Realm JSON updated:** Added `system-administrator` and `user` realm roles, `realmRoles` assignment on michael.schmidt, protocol mapper for realm roles in tokens.
- **Seed user:** Michael Schmidt (mikenging@hotmail.com) seeded as SystemAdministrator on startup. KeycloakSubjectId linked on first login.

### Multi-Tenancy (2026-04-11)

- **Architecture:** Shared database, row-level isolation. Tenants table with unique slug. Many-to-many via `UserTenantMembership` (composite key: UserId + TenantId).
- **Tenant context:** Frontend sends `X-Tenant-Id` header with every request. Middleware validates user membership and sets `HttpContext.Items["TenantId"]`.
- **Exempt paths:** `/api/users/me`, `/api/users/me/tenants`, `/api/tenants/*`, `/health`, `/alive` don't require tenant header.
- **Role model:** `UserRole` stays global (SystemAdministrator = platform-wide admin, User = regular). No per-tenant roles yet.
- **New models:** `Tenant` (Id, Name, Slug, IsActive, timestamps), `UserTenantMembership` (UserId, TenantId, JoinedAt).
- **New DTOs:** `TenantDtos.cs` — TenantResponse, CreateTenantRequest, UpdateTenantRequest, AddTenantMemberRequest, TenantMemberResponse, UserTenantResponse, SetActiveTenantRequest.
- **New endpoints:** `TenantEndpoints.cs` — full tenant CRUD (admin-only for writes), member management, `/api/users/me/tenants` for tenant selector, `/api/users/me/tenant` to set active tenant.
- **UserEndpoints updated:** `GetAllUsers` now tenant-aware — with `X-Tenant-Id`, returns only that tenant's members; without it, admins see all users.
- **Migration reset:** Deleted old migration, created fresh `InitialCreate` with all three tables (Users, Tenants, UserTenantMemberships). Use `--output-dir Data/Migrations` flag.
- **Seed data:** Default tenant "Warp Industries" (slug: `warp-industries`) seeded, Michael Schmidt added as member.

### Self-Service Profile Update (2026-04-11)

- **New endpoint:** `PUT /api/users/me` (UpdateMyProfile) — lets authenticated users update their own FirstName and LastName without admin privileges.
- **Security boundary:** Email and Role are NOT editable via self-service; those remain admin-only through `PUT /api/users/{id}`.
- **Keycloak sync:** If the user has a KeycloakSubjectId, the endpoint syncs name changes to Keycloak (passing existing email unchanged).
- **User lookup:** Reuses the same sub-claim → email fallback pattern from GetCurrentUser.
- **DTO:** `UpdateProfileRequest(string FirstName, string LastName)` added to both API and Web DTOs.
- **Frontend client:** `UpdateProfileAsync` method added to `UserApiClient.cs`.
- **Testing:** 4 comprehensive tests added by Worf (happy path, not found, email fallback, field preservation). All 56 tests pass.
- **Status:** ✅ Complete. Endpoint tested, frontend UI built, ready for production.

### Auth Token Flow Diagnostics (2026-04-11)

- **Problem:** 401 Unauthorized persists on User Management page despite previous PersistentComponentState + per-request header fix compiling cleanly. Tests pass but runtime fails — config/infrastructure issue.
- **Investigation findings:**
  - Token flow has 3 redundant capture paths: AuthTokenHandler (SSR only), TokenCircuitHandler (circuit open), PersistentComponentState (SSR→circuit bridge). All should work but no visibility into which path fires.
  - AuthTokenHandler does NOT interfere during circuit (httpContext is null → no-op). Typed client's `CreateRequest()` is the sole token source in circuit.
  - No DI scope conflict: `UserApiClient` gets circuit-scoped `TokenProvider` because typed clients resolve from the current scope (not the IHttpClientFactory handler scope).
  - API JWT validation failures were invisible — no JwtBearerEvents logging.
  - **Likely root cause:** Keycloak data volume preserves old realm config. The `oidc-audience-mapper` for `warpbusiness-api` (added later to `warpbusiness-realm.json`) may not have been applied because Keycloak skips import when realm already exists.
- **Fix:** Added comprehensive structured logging at every stage of the token flow (Web and API). Added JwtBearerEvents on API to reveal exact JWT rejection reason (issuer, audience, signature). User should delete Keycloak data volume and restart to ensure realm config is fresh.
- **Key diagnostic log prefixes:** `[JWT]`, `[AuthBase]`, `[AuthTokenHandler]`, `[TokenCircuitHandler]`, `[UserApiClient]`, `[TenantApiClient]`
- **Key files modified:**
  - `WarpBusiness.Web/Services/AuthTokenHandler.cs` — added ILogger, SSR/circuit logging, 401 response logging with WWW-Authenticate
  - `WarpBusiness.Web/Components/AuthenticatedComponentBase.cs` — added ILoggerFactory, full restore/capture flow logging
  - `WarpBusiness.Web/Services/TokenCircuitHandler.cs` — added ILogger, circuit open diagnostics
  - `WarpBusiness.Web/Services/UserApiClient.cs` — added ILogger, per-request token logging
  - `WarpBusiness.Web/Services/TenantApiClient.cs` — added ILogger, per-request token logging
  - `WarpBusiness.Api/Program.cs` — JwtBearerEvents (OnAuthenticationFailed, OnTokenValidated, OnChallenge, OnMessageReceived)
  - `WarpBusiness.Web/Program.cs` — startup URL resolution logging

### Keycloak Error Handling in User Creation (2026-04-12)

- **Root cause of 500:** `KeycloakAdminService.CreateUserAsync` returned `null` on any Keycloak failure, losing the actual error details. The endpoint then returned HTTP 502, which Aspire's standard retry policy retried 3 times — all failing — producing the "Standard-Retry, Attempt: 3, Result: 500" log pattern.
- **Fix:** Introduced `KeycloakOperationResult` record type that carries `Success`, `KeycloakUserId`, `StatusCode`, and parsed `ErrorMessage`. The `CreateUser` endpoint now maps Keycloak 409→Conflict, 4xx→400 BadRequest with detail, 5xx→502 BadGateway.
- **Keycloak error parsing:** Keycloak Admin API returns JSON with `errorMessage`, `error_description`, or `error` fields. Added `ParseKeycloakErrorMessage()` to extract human-readable messages.
- **No password policy in realm JSON:** `warpbusiness-realm.json` has no `passwordPolicy` setting, so Keycloak uses defaults. If password rejection occurs at runtime, it's due to policies configured via admin UI (persisted in data volume).
- **Key principle:** Never let Keycloak 400-level responses bubble up as 500s — they're client validation errors that should not be retried.
- **Key files:**
  - `WarpBusiness.Api/Services/KeycloakAdminService.cs` — `KeycloakOperationResult`, updated `CreateUserAsync`, `ParseKeycloakErrorMessage()`
  - `WarpBusiness.Api/Endpoints/UserEndpoints.cs` — `CreateUser` endpoint with proper status code mapping
