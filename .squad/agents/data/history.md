# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, Entity Framework Core, PostgreSQL
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Progressive Streaming + Batch Rendering Pattern (2026-04-17)

- **Integrated workflow:** API streams via `IAsyncEnumerable<T>` endpoints; Blazor UI consumes stream and renders in fixed-size batches (25 items) with live progress counter.
- **Batch rendering benefits:** Initial screen population faster (first 25 items arrive while remaining stream downloads). UI remains responsive during large list loads. No artificial pagination UI needed.
- **Post-mutation reloads:** For add/edit/delete operations, still use bulk endpoint (non-streaming), not incremental append. Reasoning: mutations are rare; load performance only matters on initial/refresh load.
- **Stream consumption:** `CatalogApiClient.GetProductsStreamAsync()` and `TaxonomyApiClient.GetProviderNodesStreamAsync()` handle header check and lazy deserialization.
- **Products.razor implementation:** Loop over stream with `foreach await` (wrapped in streaming context), append batch to list, UI re-renders each batch automatically via Blazor binding.
- **Backward compatibility:** Non-streaming bulk endpoints (`/api/catalog/products`, `/api/taxonomy/providers/{key}/nodes`) remain unchanged; streaming is opt-in via separate routes.

### IAsyncEnumerable Streaming for Products and Taxonomy APIs (2026-04-28)

- **Feature:** Prototype streaming variants for Products and Taxonomy node list endpoints. Allows the Blazor frontend to progressively render data as rows arrive from the database.
- **API endpoints added:**
  - `GET /api/catalog/products/stream` — streams all tenant products as a JSON array using `IAsyncEnumerable<ProductResponse>`
  - `GET /api/taxonomy/providers/{key}/nodes/stream` — streams all flat nodes for a provider ordered by depth then name
- **Pattern (API side):** Endpoint handler returns `IResult`. For synchronous validation (tenant check / provider lookup) use early `Results.BadRequest`/`Results.NotFound`, then build an `IAsyncEnumerable<T>` via EF Core `.AsAsyncEnumerable().Select(mapper)` and return `Results.Ok(stream)`. ASP.NET Core minimal APIs + System.Text.Json stream the JSON array progressively via `WriteAsJsonAsync`.
- **EF Core gotcha:** `AsSplitQuery()` is **incompatible** with `AsAsyncEnumerable()`. The streaming endpoint drops split-query and uses a single JOIN query instead. This is acceptable for a prototype; heavy include graphs may produce cartesian-product row bloat, so consider projections for large result sets in production.
- **Pattern (client side):** Use `HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)` to get headers without buffering the body, then `JsonSerializer.DeserializeAsyncEnumerable<T>(stream, options, ct)` to deserialize lazily. Method is `async IAsyncEnumerable<T>` with `[EnumeratorCancellation]` on the `CancellationToken` parameter.
- **Taxonomy streaming:** Streams flat nodes; tree reconstruction (parent/child hierarchy) is left to the caller. `ExternalNodeResponse` DTO is reused — server sends `Attributes` in JSON but the client DTO ignores it, which is intentional for the prototype.
- **Key files modified:**
  - `WarpBusiness.Api/Endpoints/CatalogEndpoints.cs` — `StreamProducts` handler + route registration
  - `WarpBusiness.CommonTaxonomy/Endpoints/TaxonomyEndpoints.cs` — `StreamProviderNodes` handler + route registration
  - `WarpBusiness.Web/Services/CatalogApiClient.cs` — `GetProductsStreamAsync()` method
  - `WarpBusiness.Web/Services/TaxonomyApiClient.cs` — `GetProviderNodesStreamAsync()` method
- **Build result:** 0 errors, 2 pre-existing warnings.

### Employee-User Data Synchronization (2026-04-11)

- **Feature:** When linking an existing user account to an employee record, automatically populate empty employee fields from matching user record data.
- **Scope of sync:** Three core fields are copied (FirstName, LastName, Email) — only into empty employee fields; existing employee values are never overwritten.
- **New endpoint:** `PUT /api/employees/{id}/link-user/{userId}` — links an unlinked employee to an existing tenant user with automatic data sync.
- **Validation:** Employee must be unlinked (no existing UserId), user must exist, user must be in same tenant, user cannot already be linked to another employee.
- **Helper method:** `SyncMissingDataFromUserToEmployee(ApplicationUser, Employee)` — encapsulates the conditional-copy logic for reuse.
- **Tested thoroughly:** 8 comprehensive tests (data sync happy path, preservation of existing data, all error cases: 400/404/409 validation errors).
- **Key files modified:**
  - `WarpBusiness.Api/Endpoints/EmployeeUserEndpoints.cs` — new LinkUserToEmployee endpoint + SyncMissingDataFromUserToEmployee helper
  - `WarpBusiness.Api.Tests/Endpoints/EmployeeUserLinkingTests.cs` — 8 new tests for the link-user-to-employee flow, all passing
- **Test results:** All 30 EmployeeUserLinking tests pass (includes 8 new LinkUserToEmployee tests), feature is production-ready.

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

### Logout Redirect Fix (2026-04-13)

- **Root cause:** `PostLogoutRedirectUri` was set to `"/"` (a relative path) in both the OIDC `OnRedirectToIdentityProviderForSignOut` event and the `/logout` endpoint's `AuthenticationProperties.RedirectUri`. Keycloak interpreted `"/"` as relative to itself, redirecting users to the Keycloak login page instead of back to the Warp web app.
- **Fix:** Build absolute URLs from `HttpContext.Request` (`{scheme}://{host}/`) in both locations. This works dynamically regardless of the port Aspire assigns.
- **Cleanup:** Removed all diagnostic `Console.WriteLine` statements from OIDC events, `/logout` endpoint, and startup configuration (7 statements total). Structured `ILogger` logging added previously is sufficient.
- **Key principle:** When using an external identity provider (Keycloak, Auth0, etc.), always use absolute URIs for redirect parameters. Relative paths are interpreted relative to the IdP, not the app.
- **Key file:** `WarpBusiness.Web/Program.cs`

### LoginTimeoutMinutes & Persistent Auth Sessions (2026-04-13)

- **LoginTimeoutMinutes field:** Added `int? LoginTimeoutMinutes` (default 480 / 8 hours) to `Tenant` entity, all DTOs (`TenantResponse`, `CreateTenantRequest`, `UpdateTenantRequest`), and CRUD endpoints. EF Core migration `AddLoginTimeoutMinutesToTenant` adds the column with default value.
- **Auth cookie sliding expiration:** Configured `.AddCookie()` in `WarpBusiness.Web/Program.cs` with `ExpireTimeSpan = 8h`, `SlidingExpiration = true`, `Cookie.MaxAge = 8h`. Each request within the window resets the expiration timer.
- **Offline access scope:** Added `offline_access` to OIDC scopes so Keycloak issues offline refresh tokens that survive SSO session idle timeouts.
- **Keycloak session settings:** Added to `warpbusiness-realm.json`: `ssoSessionIdleTimeout: 28800` (8h), `ssoSessionMaxLifespan: 28800` (8h), `accessTokenLifespan: 300` (5m), offline session max 30 days. Access tokens are short-lived and refreshed; the SSO session and auth cookie keep users logged in.
- **Key files:**
  - `WarpBusiness.Api/Models/Tenant.cs` — new property
  - `WarpBusiness.Api/Models/TenantDtos.cs` — updated records
  - `WarpBusiness.Api/Endpoints/TenantEndpoints.cs` — create/update/response mapping
  - `WarpBusiness.Api/Data/WarpBusinessDbContext.cs` — column default value config
  - `WarpBusiness.Api/Data/Migrations/20260413203252_AddLoginTimeoutMinutesToTenant.cs` — migration
  - `WarpBusiness.Web/Program.cs` — cookie config, offline_access scope
  - `WarpBusiness.AppHost/KeycloakConfiguration/warpbusiness-realm.json` — session timeouts

### Business Entity in CRM Module (2026-04-16)

- **Feature:** Added Business entity to the CRM module as a parent entity for Customers, enabling businesses to be tracked separately from individual customer contacts.
- **Entity structure:** Business includes Id, TenantId, Name (required), Industry, Website, Phone, Address, City, State, PostalCode, Country, Notes, IsActive, CreatedAt, UpdatedAt.
- **Entity relationships:**
  - Business.Customers → one-to-many (Business has many Customers)
  - Customer.BusinessId → nullable FK to Business with OnDelete(DeleteBehavior.SetNull) — CRITICAL: not Cascade
  - Customers can exist without a Business (BusinessId = null)
- **Database constraints:**
  - Unique index on Name + TenantId (tenant-scoped uniqueness)
  - All string fields have explicit HasMaxLength() constraints (Name=255, Industry=100, Website=500, Phone=50, Address=500, City=100, State=100, PostalCode=20, Country=100, Notes=4000)
  - IsActive defaults to true via HasDefaultValue(true)
- **API endpoints:** /api/crm/businesses (GET list with customer count per business, GET by ID, POST create, PUT update, DELETE with unlink logic)
- **Delete behavior:** DELETE endpoint checks for linked customers and returns 409 Conflict unless ?unlinkCustomers=true is provided. When true, sets all linked customers' BusinessId to null before deleting.
- **DTOs:** BusinessResponse (includes CustomerCount), CreateBusinessRequest, UpdateBusinessRequest
- **Admin endpoint:** Updated /api/admin/clear truncate statement to include crm."Businesses" BEFORE crm."Customers" (FK constraint order)
- **Key files:**
  - WarpBusiness.Crm/Data/Models/Business.cs — new entity
  - WarpBusiness.Crm/Data/Models/Customer.cs — added BusinessId and Business navigation property
  - WarpBusiness.Crm/Data/CrmDbContext.cs — DbSet, Business entity config, Customer FK config
  - WarpBusiness.Api/Endpoints/BusinessEndpoints.cs — full CRUD + delete with unlink logic
  - WarpBusiness.Api/Program.cs — registered MapBusinessEndpoints()
  - WarpBusiness.Api/Endpoints/AdminEndpoints.cs — updated truncate order
  - Migration: AddBusiness

## 2025-01-29: Implemented warp e2e CLI command

**Task:** Add `warp e2e` command for seeding test data via API

**Changes:**
- Added `Guid? BusinessId = null` to `CustomerCreateDto` and wired it in `CreateCustomer` endpoint
- Created `WarpBusiness.Cli/Data/` directory with `WordData.cs`, `NameData.cs`, `LocationData.cs` containing arrays for realistic test data generation
- Expanded `WarpApiClient` with:
  - `CreateTenantAsync(name, slug, currency)` - returns null on 409 Conflict
  - `CreateEmployeeAsync(tenantId, request)` - returns null on 409 Conflict
  - `CreateBusinessAsync(tenantId, request)` - returns null on 409 Conflict
  - `CreateCustomerAsync(tenantId, request)` - returns null on 409 Conflict
  - Result records: `TenantResult`, `EmployeeResult`, `BusinessResult`, `CustomerResult`
  - Request records: `CreateEmployeeRequest`, `CreateBusinessRequest`, `CreateCustomerRequest`
- Created `E2eCommand.cs` with:
  - Three options: `--tenantCount`, `--employeeCount`, `--customerCount` (defaults: 1, 100, 50)
  - Full seeding logic with conflict retry patterns
  - Realistic data generation: names, addresses, departments, job titles, companies, industries
- Registered `E2eCommand` in CLI `Program.cs`

**Outcomes:**
- Both API and CLI projects build successfully with no warnings
- Command supports seeding multiple tenants with realistic employee and customer data
- All API calls handle 409 Conflict gracefully with retry logic

## Learnings
- Added BusinessId to CustomerCreateDto and wired it in CreateCustomer endpoint
- Added WarpApiClient methods: CreateTenantAsync, CreateEmployeeAsync, CreateBusinessAsync, CreateCustomerAsync
- Created WarpBusiness.Cli/Data/ directory with WordData.cs, NameData.cs, LocationData.cs
- Created E2eCommand.cs with full data seeding algorithm
- System.CommandLine 2.0.0-beta4: Option<T>(string, Func<T>, string) constructor works correctly
- WarpApiClient returns null on 409 Conflict for all new Create* methods (retry pattern)
- JsonDocument parsing with TryGetProperty for optional response fields works well


## Task: Tenant Requests and Subscription Management

**Date:** 2025-04-15  
**Requested by:** Michael R. Schmidt

### Implementation Summary

1. **TenantRequest Model**
   - Created Models/TenantRequest.cs with full request tracking entity
   - Enums: TenantRequestStatus (Open, InProgress, Pending, Resolved, Closed, Cancelled)
   - Enums: TenantRequestType (General, Billing, Technical, FeatureRequest, BugReport, Onboarding)
   - Fields: Title, Description, Status, Type, AssignedToName, AssignedToUserId, Resolution, timestamps
   - Navigation properties to Tenant and ApplicationUser (AssignedTo)

2. **Tenant Model Extensions**
   - Added to Models/Tenant.cs: LogoBase64, LogoMimeType, MaxUsers, SubscriptionPlan, EnabledFeatures
   - Support for tenant branding (logo) and subscription tier management

3. **DTOs**
   - Created Models/TenantRequestDtos.cs with:
     - TenantRequestResponse (full details)
     - CreateTenantRequestRequest (Title, Description, Type)
     - UpdateTenantRequestRequest (admin update with all fields)
   - Updated Models/TenantDtos.cs with:
     - Extended TenantResponse with logo and subscription fields
     - UpdateTenantLogoRequest (LogoBase64, MimeType)
     - UpdateTenantSubscriptionRequest (MaxUsers, SubscriptionPlan, EnabledFeatures)

4. **DbContext Configuration**
   - Added TenantRequests DbSet to WarpBusinessDbContext
   - Entity config for TenantRequest:
     - Indexes on TenantId, Status, AssignedToUserId (filtered)
     - String enums stored with .HasConversion<string>()
     - FK to Tenant (Cascade), FK to ApplicationUser (SetNull)
     - Max lengths: Title (500), Description (4000), Resolution (4000), AssignedToName (200)
   - Extended Tenant entity config with logo and subscription fields

5. **API Endpoints - TenantRequestEndpoints.cs**
   - **Tenant-facing** (/api/tenants/{tenantId}/requests):
     - GET / — list requests with filters (search, status, type, assignedTo)
     - POST / — create new request
     - GET /{id} — get single request
     - PUT /{id}/cancel — cancel own request
   - **Admin-facing** (/api/admin/requests, requires SystemAdministrator role):
     - GET / — list all requests across all tenants
     - PUT /{id} — update request (status, assignment, resolution)
   - Authorization: tenant members can access their own requests, admins see all

6. **API Endpoints - Tenant Logo & Subscription**
   - Added to TenantEndpoints.cs:
     - PUT /api/tenants/{id}/logo — upload logo (validates image/* mime type)
     - DELETE /api/tenants/{id}/logo — clear logo
     - PUT /api/tenants/{id}/subscription — update subscription (admin-only)
   - Authorization: tenant members can manage their logo, admins manage subscriptions

7. **EF Migration**
   - Created migration 20260415035354_AddTenantRequestsAndLogoAndSubscription
   - Adds TenantRequests table with all indexes and foreign keys
   - Adds columns to Tenants: LogoBase64, LogoMimeType, MaxUsers, SubscriptionPlan, EnabledFeatures
   - All schema changes in "warp" schema

8. **Registration**
   - Registered MapTenantRequestEndpoints() in Program.cs
   - Updated ToResponse helper in TenantEndpoints to include new fields

### Patterns Used

- **Multi-tenant authorization**: AuthorizeTenantAccess checks membership or admin role
- **Enum storage**: enums stored as strings in DB (.HasConversion<string>())
- **Filtered indexes**: AssignedToUserId index filtered to only non-null values
- **Cascade deletes**: requests deleted when tenant deleted, assigned user set null on delete
- **Status transitions**: only Open/Pending requests can be cancelled
- **Denormalization**: AssignedToName stored for display (optional navigation to ApplicationUser)
- **Query filters**: support for search, status, type, assignedTo, tenantId filters
- **Timestamp tracking**: CreatedAt, UpdatedAt, ClosedAt (set on final status)

### Build Status

✅ API project builds successfully with no warnings


### MinIO Storage Integration (2026-04-15)

- **New library:** `WarpBusiness.Storage` — reusable file storage abstraction backed by MinIO. Target framework: net10.0, depends on `Minio` NuGet package v6.0.4.
- **Service interface:** `IFileStorageService` — UploadAsync, GetPresignedUrlAsync, DeleteAsync, EnsureBucketExistsAsync. All methods take bucket + objectKey parameters, return Task. Stream-based upload with optional content length.
- **Implementation:** `MinioFileStorageService` — uses `IMinioClient` from Minio SDK. Wraps all Minio exceptions in `InvalidOperationException` with meaningful messages. Singleton registration.
- **DI registration:** `AddMinioStorage(IConfiguration)` extension method parses MinIO connection string (`http://accessKey:secretKey@endpoint:port`), registers `IMinioClient` and `IFileStorageService` as singletons.
- **Aspire integration:** `CommunityToolkit.Aspire.Hosting.Minio` v13.1.1 in AppHost. Method is `AddMinioContainer(name)` (not `AddMinio`). Returns `IResourceBuilder<MinioContainerResource>` with `.WithDataVolume()` for persistence. Requires `using CommunityToolkit.Aspire.Hosting;` directive.
- **Bucket strategy:** Hosted service `StorageBucketInitializer` ensures `warp-catalog` and `warp-logos` buckets exist at startup. Runs before HTTP pipeline starts, fails fast on error.
- **Object key convention:** Products: `products/{productId}/image.jpg`, Variants: `variants/{variantId}/image.jpg`, Tenant logos: `tenants/{tenantId}/logo`.
- **AppHost wiring:** MinIO container added with `.WithDataVolume("minio-data")` for persistence. API project has `.WithReference(minio).WaitFor(minio)` for startup ordering. Connection string automatically injected by Aspire as `ConnectionStrings:minio`.
- **Key files:**
  - `WarpBusiness.Storage/IFileStorageService.cs` — service interface
  - `WarpBusiness.Storage/MinioFileStorageService.cs` — MinIO implementation
  - `WarpBusiness.Storage/StorageServiceExtensions.cs` — DI registration
  - `WarpBusiness.Storage/StorageBucketInitializer.cs` — startup bucket creation
  - `WarpBusiness.AppHost/AppHost.cs` — MinIO container registration
  - `WarpBusiness.Api/Program.cs` — service registration + hosted service
- **Build status:** ✅ Solution builds successfully. Storage library compiles cleanly, AppHost updated, API wired.
### Catalog Image Storage Integration (2026-04-15)

- **Feature:** Added image storage support to the Catalog module using the WarpBusiness.Storage library with MinIO backend.
- **Schema changes:** Added nullable `ImageKey` property (max 500 chars) to both `Product` and `ProductVariant` models. EF migration `AddImageKey` created.
- **Storage pattern:** Object keys follow `{tenantId}/products/{productId}/{uuid}.{ext}` for products and `{tenantId}/variants/{variantId}/{uuid}.{ext}` for variants. Bucket: `warp-catalog`.
- **Image upload endpoints:** `POST /api/catalog/products/{productId}/image` and `POST /api/catalog/variants/{variantId}/image` — accept `IFormFile`, validate content type (image/jpeg, image/png, image/gif, image/webp), enforce 5MB limit, delete old image before uploading new, update entity with new key.
- **Image delete endpoints:** `DELETE /api/catalog/products/{productId}/image` and `DELETE /api/catalog/variants/{variantId}/image` — remove image from MinIO and clear ImageKey from entity.
- **Image proxy endpoint:** `GET /api/catalog/images/{*objectKey}` — returns presigned MinIO URL with 1-hour expiry, allows anonymous access (URLs are time-limited tokens).
- **Response DTOs updated:** Added `ImageKey` to `ProductResponse` and `ProductVariantResponse` so frontend can display images.
- **Multi-tenancy:** TenantId prefix in object keys ensures tenant isolation. Validates product/variant belongs to tenant before upload/delete.
- **Key files:**
  - `WarpBusiness.Catalog/Models/Product.cs` — added ImageKey property
  - `WarpBusiness.Catalog/Models/ProductVariant.cs` — added ImageKey property
  - `WarpBusiness.Catalog/Data/CatalogDbContext.cs` — added ImageKey EF config (max 500 chars)
  - `WarpBusiness.Catalog/Data/Migrations/*AddImageKey*.cs` — EF migration
  - `WarpBusiness.Api/Endpoints/CatalogImageEndpoints.cs` — new endpoint file with upload/delete/proxy logic
  - `WarpBusiness.Api/Endpoints/CatalogEndpoints.cs` — updated DTOs with ImageKey parameter
  - `WarpBusiness.Api/Program.cs` — registered CatalogImageEndpoints
- **Build status:** ✅ API project builds successfully. Frontend build fails due to missing UI implementation (Geordi's responsibility).
- **Next step:** Geordi needs to implement frontend image upload UI that references the missing methods (`GetProductImageUrl`, `HandleProductImageUpload`, `RemoveProductImage`, `ShowVariantImageUpload`, `CloseVariantImageModal`, `HandleVariantImageUpload`, `RemoveVariantImage`) in `Products.razor`.

### Catalog Image and Video Storage Integration (2026-04-15)

- **Feature:** Added image and video storage support to the Catalog module using the WarpBusiness.Storage library with MinIO backend.
- **Schema changes:** Added nullable `ImageKey` and `VideoKey` properties (max 500 chars each) to both `Product` and `ProductVariant` models. EF migration `AddImageAndVideoKeys` created.
- **Storage pattern:** Object keys follow tenant-prefixed pattern:
  - Product images: `{tenantId}/products/{productId}/{uuid}.{ext}`
  - Variant images: `{tenantId}/variants/{variantId}/{uuid}.{ext}`
  - Product videos: `{tenantId}/products/{productId}/videos/{uuid}.{ext}` (note "videos" subdirectory)
  - Variant videos: `{tenantId}/variants/{variantId}/videos/{uuid}.{ext}`
  - All use same bucket: `warp-catalog`
- **Image upload endpoints:** `POST /api/catalog/products/{productId}/image` and `POST /api/catalog/variants/{variantId}/image` — accept `IFormFile`, validate content type (image/jpeg, image/png, image/gif, image/webp), enforce 5MB limit, delete old image before uploading new, update entity with new key.
- **Video upload endpoints:** `POST /api/catalog/products/{productId}/video` and `POST /api/catalog/variants/{variantId}/video` — accept `IFormFile`, validate content type (video/mp4, video/webm, video/quicktime, video/x-msvideo), enforce 500MB limit, delete old video before uploading new, update entity with new key.
- **Delete endpoints:** `DELETE` routes for both images and videos — remove from MinIO and clear key from entity.
- **Proxy endpoints:** 
  - `GET /api/catalog/images/{*objectKey}` — returns presigned MinIO URL with 1-hour expiry
  - `GET /api/catalog/videos/{*objectKey}` — returns presigned MinIO URL with 24-hour expiry (longer for video streaming)
  - Both allow anonymous access (URLs are time-limited tokens)
- **Response DTOs updated:** Added `ImageKey` and `VideoKey` to `ProductResponse` and `ProductVariantResponse` so frontend can display media.
- **Multi-tenancy:** TenantId prefix in object keys ensures tenant isolation. Validates product/variant belongs to tenant before upload/delete.
- **Key files:**
  - `WarpBusiness.Catalog/Models/Product.cs` — added ImageKey and VideoKey properties
  - `WarpBusiness.Catalog/Models/ProductVariant.cs` — added ImageKey and VideoKey properties
  - `WarpBusiness.Catalog/Data/CatalogDbContext.cs` — added ImageKey and VideoKey EF config (max 500 chars each)
  - `WarpBusiness.Catalog/Data/Migrations/*AddImageAndVideoKeys*.cs` — EF migration
  - `WarpBusiness.Api/Endpoints/CatalogImageEndpoints.cs` — endpoint file with image/video upload/delete/proxy logic (493 lines)
  - `WarpBusiness.Api/Endpoints/CatalogEndpoints.cs` — updated DTOs with ImageKey and VideoKey parameters
  - `WarpBusiness.Api/Program.cs` — registered CatalogImageEndpoints
- **Build status:** ✅ API project builds successfully. Frontend build fails due to missing UI implementation (Geordi's responsibility).
- **Next step:** Geordi needs to implement frontend media upload UI that references the missing methods in `Products.razor` for both images and videos.

### Catalog Warnings Renamed to Notations (2026-04-16)

- **Feature:** Renamed the "Warnings" concept to "Notations" throughout the Catalog backend and database schema. Upgraded the free-text Icon field to a strongly-typed NotationIcon enum.
- **NotationIcon enum:** Created WarpBusiness.Catalog/Models/NotationIcon.cs with 16 values, each mapped to a Bootstrap Icons CSS class:
  - None, Warning (bi-exclamation-triangle-fill), Info (bi-info-circle-fill), Note (bi-journal-text), Caution (bi-exclamation-circle-fill), Danger (bi-x-octagon-fill)
  - Prohibited (bi-slash-circle), Flammable (bi-fire), Chemical (bi-droplet-fill), ElectricalHazard (bi-lightning-fill), Recyclable (bi-recycle)
  - EcoFriendly (bi-leaf-fill), FoodAllergen (bi-egg-fill), Prop65 (bi-exclamation-diamond-fill), Compliance (bi-shield-check-fill), Temperature (bi-thermometer-half)
- **Model changes:**
  - CatalogWarning → CatalogNotation: Icon property changed from string? to NotationIcon?, stored as string enum with max length 50
  - ProductWarning → ProductNotation: WarningId → NotationId, Warning nav → Notation nav
  - Product model: Warnings collection → Notations
- **Database migration:** Created RenameWarningsToNotations migration (20260416044001) that uses RenameTable and RenameColumn to preserve existing data. Icon column type changed from text to varchar(50).
- **API endpoints:**
  - CatalogWarningEndpoints.cs → CatalogNotationEndpoints.cs
  - Routes changed: /api/catalog/warnings → /api/catalog/notations, /api/catalog/products/{id}/warnings → /api/catalog/products/{id}/notations
  - DTOs: WarningResponse → NotationResponse, CreateWarningRequest → CreateNotationRequest, UpdateWarningRequest → UpdateNotationRequest
  - ProductWarningResponse → ProductNotationResponse with NotationIcon? Icon instead of string? Icon
- **CatalogEndpoints.cs updates:** All product CRUD operations updated to use NotationIds instead of WarningIds, reference db.Notations/ProductNotations instead of db.Warnings/ProductWarnings
- **Program.cs:** MapCatalogWarningEndpoints() → MapCatalogNotationEndpoints()
- **Migration strategy:** The generated Drop+Create migration was manually edited to use RenameTable/RenameColumn/RenameIndex to preserve existing data. Existing Icon values (emoji strings) will not map to enum values but remain in DB (acceptable in dev).
- **Files deleted:** CatalogWarning.cs, ProductWarning.cs, CatalogWarningEndpoints.cs (replaced by new Notation equivalents)
- **Build status:** ✅ WarpBusiness.Catalog and WarpBusiness.Api both build successfully with no errors.
- **Key files:**
  - WarpBusiness.Catalog/Models/NotationIcon.cs (new)
  - WarpBusiness.Catalog/Models/CatalogNotation.cs (renamed from CatalogWarning.cs)
  - WarpBusiness.Catalog/Models/ProductNotation.cs (renamed from ProductWarning.cs)
  - WarpBusiness.Catalog/Data/CatalogDbContext.cs (updated)
  - WarpBusiness.Catalog/Migrations/20260416044001_RenameWarningsToNotations.cs (new)
  - WarpBusiness.Api/Endpoints/CatalogNotationEndpoints.cs (renamed from CatalogWarningEndpoints.cs)
  - WarpBusiness.Api/Endpoints/CatalogEndpoints.cs (updated all Warning refs to Notation)
  - WarpBusiness.Api/Program.cs (updated endpoint registration)


## 2026-04-16 — Notations Rename

**Timestamp:** 2026-04-16T04:47:14Z

Completed full backend Warning→Notation rename:
- Added NotationIcon enum (16 values, mapped to Bootstrap Icons)
- Renamed CatalogWarning→CatalogNotation, ProductWarning→ProductNotation
- Created data-preserving migration: RenameWarningsToNotations
- Updated API routes: /api/catalog/warnings→/api/catalog/notations
- Updated CatalogEndpoints.cs for notation CRUD

**Build:** ✅ 0 errors, 5 pre-existing warnings

**Handoff:** Ready for Geordi to update frontend API client and UI components.

## Task: nginx Reverse Proxy for Subdomain Routing

**Date:** 2026-04-16  
**Requested by:** Michael R. Schmidt

### Implementation Summary

Added an nginx reverse proxy container to the Aspire AppHost for subdomain-based routing in preparation for warp-business.com DNS wiring.

**Files created:**
- `nginx/nginx.conf.template` — subdomain routing config using `${VAR}` envsubst placeholders, one `server {}` block per subdomain, with `/_health` location and WebSocket support on Blazor/portal services
- `nginx/README.md` — routing table, env var reference, production usage guide

**Files modified:**
- `WarpBusiness.AppHost/AppHost.cs` — captured web, customerPortal, tenantPortal, marketingSite as named variables; added nginx container with `nginx:alpine`, bind-mount of template, port 80 endpoint, all upstream env vars injected via `GetEndpoint("http")`, and `WaitFor` all upstream services

**AppHost pattern used:**
```csharp
builder.AddContainer("nginx", "nginx", "alpine")
    .WithBindMount("../nginx/nginx.conf.template", "/etc/nginx/templates/nginx.conf.template")
    .WithHttpEndpoint(targetPort: 80, name: "http")
    .WithEnvironment("API_UPSTREAM", api.GetEndpoint("http"))
    ...
```

## Learnings
- nginx docker's `/etc/nginx/templates/` directory is the idiomatic entrypoint for envsubst templating — files placed there are processed at container start, output written to `/etc/nginx/conf.d/`
- `AddContainer(name, image, tag)` is the correct three-argument Aspire overload for containers with explicit tags (e.g., `nginx:alpine`)
- Blazor Server and portals need `Upgrade`/`Connection` WebSocket headers in proxy config; marketing and API do not
- All four portal/web projects needed to be captured as `var` to enable `GetEndpoint()` calls — previously only `api` was assigned to a variable
- `WaitFor` on all upstreams prevents nginx from starting before backends are ready (avoids 502 on first request)
- `set -xe` in the shell entrypoint traces every command to stderr before execution and exits on first error — essential for debugging silent container failures
- `nginx -t` before starting nginx surfaces config syntax errors in container logs instead of a silent crash
- `exec nginx` replaces the shell process, giving nginx PID 1 and proper SIGTERM/SIGINT signal handling for clean shutdown
- Duplicate `proxy_set_header Connection` directives (first `""` then `"upgrade"`) cause config errors in some nginx versions; WebSocket blocks need only the `"upgrade"` version
- WarpBusiness.Taxonomy uses its own `taxonomy` schema with TaxonomyNode, ExternalTaxonomyCache, and ExternalTaxonomyNode entities; nodes store materialized paths based on slugged names for hierarchy navigation.
- Taxonomy providers include Google (public), Amazon PA-API (credential-gated), eBay OAuth, and Etsy API key; downloads are orchestrated by TaxonomyDownloadService and imports are handled by TaxonomyImportService with source tracking.
- Provider keys for taxonomy sources are now strings (static constants) instead of enums, keeping the model open for extension without schema changes.

### Taxonomy: Targeted Import + Cascade Delete (2026-04-17)

- **Feature 1 (targeted import):** `TaxonomyImportService.ImportAsync` already accepted `Guid? targetParentId` and `ImportNodesRequest` already carried `TargetParentId`. Feature was fully implemented — verified, no changes required.
- **Feature 2 (cascade delete):** New `DELETE /api/taxonomy/nodes/{id}?cascade=true` endpoint added in `WarpBusiness.Api/Endpoints/TaxonomyNodeEndpoints.cs`.
  - Loads the target node and all descendants via `MaterializedPath LIKE '{path}/%'` prefix query.
  - Returns `400 Bad Request` if node has children and `cascade=false`.
  - Runs catalog conflict check stub (`GetCatalogConflictsAsync`) — returns empty until catalog gains a `TaxonomyNodeId` FK.
  - Returns `409 Conflict` with `conflictingNodeIds` array if any nodes in the subtree are in use.
  - Deletes subtree in a single `RemoveRange` + `SaveChangesAsync`.
- **New GET endpoints:** `GET /api/taxonomy/nodes/roots` (root-level nodes for tenant) and `GET /api/taxonomy/nodes/{id}/children` (direct children for lazy-load tree picker) — both added to the same endpoint file.
- **Architecture decision:** New endpoints placed in `WarpBusiness.Api/Endpoints/TaxonomyNodeEndpoints.cs` (not in the Taxonomy library) to allow injection of both `TaxonomyDbContext` and `CatalogDbContext` without creating a cross-module project reference.
- **Registration:** `app.MapTaxonomyNodeEndpoints()` added to `Program.cs` after `app.MapTaxonomyEndpoints()`.
- **Key files:**
  - `WarpBusiness.Api/Endpoints/TaxonomyNodeEndpoints.cs` (new)
  - `WarpBusiness.Api/Program.cs` (updated)
- **Build status:** ✅ 0 errors, 2 pre-existing warnings.

### Blazor SSR Timeout Fix: OnAfterRenderAsync for Data Loading (2026-04-17)

- **Problem:** `TaxonomyImport.razor` timed out on page load with "The operation didn't complete within the allowed timeout of '00:00:30'" because `OnAuthenticatedInitializedAsync` ran API calls during SSR prerender — before the Blazor circuit connected, when the API might not be reachable.
- **Fix:** Moved all API calls (`LoadProviderStatusAsync`, `LoadExternalNodesAsync`) out of `OnAuthenticatedInitializedAsync` and into `OnAfterRenderAsync(bool firstRender)`. `OnAfterRenderAsync` is **never called during SSR prerender** — it only fires after the interactive Blazor circuit connects.
- **Pattern:**
  - `OnAuthenticatedInitializedAsync` → tenant redirect check only (no API calls). Returns `Task.CompletedTask`.
  - `OnAfterRenderAsync(firstRender: true)` → all API calls, then `isLoading = false`, then `StateHasChanged()`.
  - SSR phase renders immediately with spinner (`isLoading = true` default).
  - Interactive phase loads data and re-renders via `StateHasChanged()`.
- **Key rule:** In Blazor Server with `@rendermode InteractiveServer`, `OnInitializedAsync` / `OnAuthenticatedInitializedAsync` run **twice** (SSR + interactive). Any code that must only run in the interactive phase (API calls, timers, JS interop) belongs in `OnAfterRenderAsync(firstRender: true)`.
- **Backend fix:** Changed `NodeCount = p.Nodes.Count` to `NodeCount = db.Nodes.Count(n => n.ProviderId == p.Id)` in `GetProviders` — makes the correlated COUNT subquery explicit rather than relying on EF navigation property translation.
- **Key files:**
  - `WarpBusiness.Web/Components/Pages/Catalog/TaxonomyImport.razor`
  - `WarpBusiness.CommonTaxonomy/Endpoints/TaxonomyEndpoints.cs`
- **Build status:** ✅ 0 errors, 2 pre-existing warnings.
