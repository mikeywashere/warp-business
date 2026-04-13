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

### Keycloak Admin Password Wiring (2026-04-12)

- **Root cause of 401:** AppHost passed `Keycloak__AdminUser` but not `Keycloak__AdminPassword`. Aspire's `AddKeycloak()` generates a random password, so the API defaulted to "admin" which was wrong.
- **Fix:** `keycloak.Resource.AdminPasswordParameter` exposes the generated password as a `ParameterResource`. Passed it via `.WithEnvironment("Keycloak__AdminPassword", keycloak.Resource.AdminPasswordParameter)`.
- **Error handling:** `EnsureAccessTokenAsync` now logs the full response body before throwing on token acquisition failure, making credential mismatches immediately diagnosable.
- **Aspire Keycloak API:** `KeycloakResource` exposes `AdminUserNameParameter` and `AdminPasswordParameter` properties. Both are `ParameterResource` instances that work directly with `WithEnvironment()`.
- **Key files:**
  - `WarpBusiness.AppHost/AppHost.cs` — added admin password environment variable
  - `WarpBusiness.Api/Services/KeycloakAdminService.cs` — improved error logging in `EnsureAccessTokenAsync`

### Keycloak Realm Config Fixes (2026-04-12)

- **Logout redirect:** Added `"attributes": { "post.logout.redirect.uris": "+" }` to `warpbusiness-web` client. Without this, Keycloak rejects `post_logout_redirect_uri` from .NET's OIDC SignOut, leaving users stranded on the Keycloak logout page.
- **Role name alignment:** Changed realm role from `system-administrator` (kebab-case) to `SystemAdministrator` (PascalCase) to match `<AuthorizeView Roles="SystemAdministrator">` in Blazor and the `UserRole.SystemAdministrator` enum in the API. Updated both `roles.realm` and `users[0].realmRoles`.
- **Role claim path:** Changed protocol mapper `claim.name` from `realm_access.roles` to `roles`. The nested path produced JSON like `{ "realm_access": { "roles": [...] } }` which .NET OIDC middleware can't auto-map to role claims. The flat `roles` claim is directly parseable.
- **Important:** Keycloak data volume must be deleted and the container restarted for realm JSON changes to take effect (Keycloak skips import when realm already exists).
- **Key file:** `WarpBusiness.AppHost/KeycloakConfiguration/warpbusiness-realm.json`

### Keycloak User Update Fix (2026-04-12)

- **Root cause:** `UpdateUserAsync` sent `username = email` in the PUT payload, but the realm has `editUsernameAllowed: false`. Keycloak rejected the entire update with `error-user-attribute-read-only`.
- **Silent failure:** Both `UpdateMyProfile` and admin `UpdateUser` endpoints called `UpdateUserAsync` but ignored its `false` return value, so the UI showed "updated successfully" while Keycloak rejected the change.
- **Fix:** Removed `username` from the update payload (it's set at creation time and never changes). Both endpoints now check the return value and return `Results.Problem(...)` on failure.
- **Key principle:** Always check return values from Keycloak admin operations. Never send read-only fields in update payloads.
- **Key files:**
  - `WarpBusiness.Api/Services/KeycloakAdminService.cs` — removed `username` from `UpdateUserAsync` payload
  - `WarpBusiness.Api/Endpoints/UserEndpoints.cs` — added return-value checks in `UpdateMyProfile` and `UpdateUser`

### Database Schema Namespacing (2026-04-12)

- **Architecture:** All tables now live under the `warp` PostgreSQL schema instead of `public`. This is the "system" schema for common/shared tables.
- **Implementation:** `modelBuilder.HasDefaultSchema("warp")` in `WarpBusinessDbContext.OnModelCreating`. EF Core handles schema qualification in all generated SQL automatically.
- **Migration:** `MoveToWarpSchema` creates the schema and moves existing tables. Fully reversible.
- **Future pattern:** New modules will get their own schemas (e.g., `billing`, `inventory`), each potentially with its own DbContext and `HasDefaultSchema()`. This keeps the database organized as the application grows.
- **Key files:**
  - `WarpBusiness.Api/Data/WarpBusinessDbContext.cs` — `HasDefaultSchema("warp")`
  - `WarpBusiness.Api/Data/Migrations/20260412043525_MoveToWarpSchema.cs` — schema migration
- **PR:** #10

### Logout id_token_hint Fix (2026-04-12)

- **Root cause:** The `/logout` endpoint signed out of the cookie scheme first (`CookieAuthenticationDefaults.AuthenticationScheme`), which destroyed the authentication ticket containing all saved tokens. When the OIDC sign-out ran next, it couldn't find the `id_token` to send as `id_token_hint` to Keycloak, resulting in a "Missing parameters: id_token_hint" error page.
- **Fix:** Capture `id_token` via `context.GetTokenAsync("id_token")` **before** any sign-out call. Then sign out of cookies, then pass the captured token to the OIDC sign-out via both `AuthenticationProperties.Items["id_token_hint"]` and `StoreTokens()` (the OIDC handler checks both locations).
- **Key principle:** When implementing OIDC logout, always extract tokens from the auth ticket before destroying it. The order of sign-out calls matters — cookie sign-out destroys the ticket, OIDC sign-out reads from it.
- **Key file:** `WarpBusiness.Web/Program.cs` — `/logout` endpoint
- **PR:** #11

### Tenant Assignment on User Creation (2026-04-12)

- **Feature:** Add User form now supports optional tenant assignment. Users can be assigned to a tenant during creation, automatically creating a UserTenantMembership record.
- **Validation ordering:** Tenant validation happens BEFORE Keycloak user creation to avoid orphaned Keycloak users when an invalid tenant ID is provided. Flow: (1) check email uniqueness, (2) validate tenant exists if provided, (3) create in Keycloak, (4) create ApplicationUser in DB, (5) create UserTenantMembership if tenant was specified.
- **Error handling:** Returns `400 BadRequest` with message "The specified tenant does not exist." if an invalid tenant ID is provided, preventing the user creation entirely.
- **DTOs updated:** Both API (`WarpBusiness.Api/Models/UserDtos.cs`) and Web (`WarpBusiness.Web/Services/UserApiClient.cs`) `CreateUserRequest` records now include `Guid? TenantId = null` optional parameter.
- **Key files:**
  - `WarpBusiness.Api/Models/UserDtos.cs` — added TenantId to CreateUserRequest
  - `WarpBusiness.Api/Endpoints/UserEndpoints.cs` — tenant validation and membership creation in CreateUser endpoint
  - `WarpBusiness.Web/Services/UserApiClient.cs` — added TenantId to CreateUserRequest

### Multi-Tenant User Onboarding (2026-04-12)

- **Backend:** API CreateUser endpoint accepts TenantId, validates tenant exists before user creation, creates UserTenantMembership record automatically.
- **Frontend:** Add User form now requires tenant selection with type-ahead dropdown (TenantApiClient integration).
- **Integration:** Data + Geordi collaboration completed; PR #12 (Geordi) merged. Backend API ready for production.
- **Status:** ✅ Complete and tested.

### Orphaned Keycloak User Recovery (2026-04-12)

- **Problem:** If a user exists in Keycloak but not in the local warp.Users table (orphaned from a partial creation), the CreateUser endpoint returned 409 and blocked re-adding the user.
- **Fix:** When Keycloak returns 409 Conflict, the endpoint now: (1) looks up the existing Keycloak user by email via `GetUserByEmailAsync`, (2) checks if they exist in local DB by KeycloakSubjectId or email, (3) if missing locally → creates the ApplicationUser + UserTenantMembership linking to the existing Keycloak ID, (4) if already in both systems → returns the real duplicate error.
- **Pattern:** "Adopt orphan" — when an external system has a record our DB doesn't, link to it rather than failing. This avoids requiring manual Keycloak admin intervention.
- **Key principle:** The `ILogger<KeycloakAdminService>` was added to the CreateUser endpoint signature for structured logging of the adoption event. ASP.NET Core minimal API DI injects it automatically; tests must pass it via reflection (use `NullLogger<T>.Instance`).
- **Key files:**
  - `WarpBusiness.Api/Endpoints/UserEndpoints.cs` — orphan recovery in CreateUser 409 handler
  - `WarpBusiness.Api.Tests/Endpoints/UserEndpointTests.cs` — updated CallCreateUser helper
- **PR:** #14

### Employee Module (2026-04-12)

- **Architecture:** Separate class library `WarpBusiness.Employees` with its own `EmployeeDbContext` and PostgreSQL schema (`employees`). Same `warpdb` connection string, different schema — follows the per-module schema isolation pattern established in the Database Schema Namespacing decision.
- **Entity model:** Employee with standard HR fields — EmployeeNumber (auto-generated EMP00001 format, unique per tenant), name fields, email (unique), phone, DOB, hire/termination dates, department, job title, self-referencing ManagerId for org hierarchy, EmploymentStatus/EmploymentType enums stored as strings, optional UserId link to warp.Users, required TenantId.
- **DbInitializer pattern:** `EmployeeDbInitializer` (IHostedService) runs migrations on startup, consistent with existing `DbInitializer`.
- **Endpoints:** Minimal API under `/api/employees` — full CRUD, tenant-scoped via X-Tenant-Id header, SystemAdministrator policy for writes, authenticated for reads. Consistent with UserEndpoints and TenantEndpoints patterns.
- **Employee number generation:** Sequential per tenant (EMP00001, EMP00002...) via MAX query on existing records.
- **Validation:** Manager must exist in same tenant; email unique within tenant; can't self-manage.
- **Npgsql version:** `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1 (latest stable for net10.0 — 10.0.5 does not exist).
- **Key files:**
  - `WarpBusiness.Employees/Models/Employee.cs` — entity + enums
  - `WarpBusiness.Employees/Models/EmployeeDtos.cs` — CRUD DTOs
  - `WarpBusiness.Employees/Data/EmployeeDbContext.cs` — EF Core context with `employees` schema
  - `WarpBusiness.Employees/Data/EmployeeDbInitializer.cs` — migration runner
  - `WarpBusiness.Employees/Endpoints/EmployeeEndpoints.cs` — minimal API endpoints
  - `WarpBusiness.Employees/Data/Migrations/` — InitialCreate migration
- **PR:** #15

### Employee-User Account Linking (2026-04-13)

- **Feature:** Link Employee and User accounts with `once linked, always linked` semantics
- **Architecture decisions:**
  - New `EmployeeUserEndpoints.cs` in `WarpBusiness.Api/Endpoints/` — combined endpoints needing both DbContexts
  - Both `WarpBusinessDbContext` (warp schema) and `EmployeeDbContext` (employees schema) share the same PostgreSQL database via Aspire `warpdb`
  - `KeycloakAdminService` extended with passwordless user creation (`CreateUserWithoutPasswordAsync`) and `SendRequiredActionsEmailAsync`
  - Filtered unique index on `Employee.UserId` (`WHERE UserId IS NOT NULL`) enforces one-user-one-employee globally
  - Email index changed from global unique to tenant-scoped (`Email + TenantId`)
- **Data integrity rules:**
  - Employee deletion blocked if `UserId` is set (400)
  - User deletion blocked if any employee has that `UserId` (400)
  - Once `Employee.UserId` is set, `UpdateEmployee` rejects changes to it (400)
- **Key files:**
  - `WarpBusiness.Api/Endpoints/EmployeeUserEndpoints.cs` — `/api/users/unlinked`, `/api/employees/with-user`, `/api/employees/{id}/with-user`, `/api/employees/by-user/{userId}`
  - `WarpBusiness.Api/Models/EmployeeUserDtos.cs` — `CreateEmployeeWithUserRequest`, `UpdateEmployeeWithUserRequest`
  - `WarpBusiness.Api/Services/KeycloakAdminService.cs` — added `CreateUserWithoutPasswordAsync`, `SendRequiredActionsEmailAsync`
  - `WarpBusiness.Api/Endpoints/UserEndpoints.cs` — `LinkedEmployeeId` added to responses, delete blocked if linked
  - `WarpBusiness.Api/Models/UserDtos.cs` — `UserResponse` and `UserWithTenantsResponse` include `LinkedEmployeeId`
  - `WarpBusiness.Employees/Endpoints/EmployeeEndpoints.cs` — immutability guard on `UserId`, delete blocked if linked
  - `WarpBusiness.Employees/Data/EmployeeDbContext.cs` — new indexes
  - Migration: `AddUserIdUniqueAndTenantScopedEmail`
