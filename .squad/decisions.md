# Squad Decisions

## Active Decisions

### Decision: Keycloak Authentication Architecture

**Date:** 2026-04-11
**Author:** Data (Backend Dev)
**Status:** Active

#### Context

The project needed authentication and authorization. Keycloak was chosen as the identity provider, integrated via .NET Aspire's official hosting and authentication packages.

#### Decision

##### Identity Provider: Keycloak via Aspire

- Keycloak runs as an Aspire container resource on port 8080 (pinned for OIDC cookie stability).
- Data volume ensures state persists across `dotnet run` restarts.
- Realm import from `WarpBusiness.AppHost/KeycloakConfiguration/` auto-provisions the `warpbusiness` realm on first start.

##### Two Clients

1. **warpbusiness-web** — Public OIDC client for the Blazor frontend. Standard flow + direct access grants enabled. Redirect URIs set to `*` for development.
2. **warpbusiness-api** — Bearer-only client for the API. No interactive login.

##### API Authentication

- Uses `Aspire.Keycloak.Authentication` package with `AddKeycloakJwtBearer()`.
- Connection string to Keycloak is resolved via Aspire service discovery (the `"keycloak"` resource name).
- Weatherforecast endpoint requires authorization as proof of integration.

##### Package Versions

- Both Keycloak packages are at `13.2.2-preview.1.26207.2` (preview — no stable 13.2.2 release yet for Keycloak components).

#### Consequences

- ✅ Zero manual Keycloak setup — realm, clients, and test user are provisioned automatically.
- ✅ Aspire service discovery handles Keycloak URL resolution for both API and Web.
- ✅ Bearer-only API client means the API never handles login flows.
- ⚠️ Preview packages — monitor for stable release and update when available.
- ⚠️ Wildcard redirect URIs must be locked down before production.

### Decision: OIDC Authentication in Blazor Web App

**Date:** 2026-04-11
**Author:** Geordi (Frontend Dev)
**Status:** Active

#### Context

The Web project needed OIDC authentication against a Keycloak identity provider being set up by Data in the AppHost.

#### Decision

Used standard `Microsoft.AspNetCore.Authentication.OpenIdConnect` (v10.0.5) rather than a third-party Aspire Keycloak client package, since no official one exists.

##### Key Choices

- **Cookie + OIDC dual scheme**: Cookie as default scheme, OpenIdConnect as challenge scheme
- **Keycloak URL from Aspire config**: Reads `services:keycloak:https:0` / `services:keycloak:http:0` — compatible with Aspire service discovery
- **Minimal API login/logout**: `/login` triggers OIDC challenge, `/logout` signs out of both cookie and OIDC
- **CascadingAuthenticationState via DI**: Uses `AddCascadingAuthenticationState()` instead of wrapping in `<CascadingAuthenticationState>` component
- **NameClaimType set to `preferred_username`**: Keycloak's standard claim for user display name

#### Rationale

- Standard OIDC package is well-supported, framework-aligned, and avoids third-party dependency risk
- Minimal API endpoints for login/logout are simpler than Razor pages and don't need antiforgery
- DI-based cascading auth state is the modern .NET 8+ Blazor pattern

#### Consequences

- ✅ Clean integration with Aspire service discovery
- ✅ No extra third-party dependencies
- ⚠️ Keycloak realm `warpbusiness` and client `warpbusiness-web` must be configured to match (Data's responsibility in AppHost)
- ⚠️ `RequireHttpsMetadata` is disabled in Development — acceptable for local Keycloak

### Decision: .NET Aspire Project Structure

**Date:** 2026-04-11  
**Author:** Riker (Lead)  
**Status:** Active

#### Context

Michael requested a .NET Aspire application with a Blazor web frontend, an ASP.NET Core Web API middle tier, and a PostgreSQL database. The architecture needed to support modern cloud-native development with built-in observability, service discovery, and orchestration.

#### Decision

We have created a multi-project .NET Aspire solution with the following structure:

##### Projects

1. **WarpBusiness.AppHost** - The Aspire orchestrator
   - Manages all services and resources
   - Configures PostgreSQL with PgAdmin
   - References and wires up API and Web projects
   - Entry point for running the entire application stack

2. **WarpBusiness.ServiceDefaults** - Shared configuration library
   - Provides common Aspire defaults (health checks, telemetry, resilience)
   - Referenced by all service projects (API and Web)
   - Centralizes cross-cutting concerns

3. **WarpBusiness.Api** - Middle tier API
   - ASP.NET Core Web API with minimal APIs
   - Weather forecast endpoint as starter template
   - References ServiceDefaults for Aspire integration
   - Will connect to PostgreSQL database

4. **WarpBusiness.Web** - Frontend application
   - Blazor Web App with interactive server components
   - References ServiceDefaults for Aspire integration
   - Configured to call the API for data

##### Database Strategy

- **PostgreSQL** chosen for relational data storage
- Provisioned via Aspire's `AddPostgres()` with PgAdmin included
- Database named "warpdb"
- API project will reference the database resource

##### Service Communication

- Web → API: Service-to-service communication via Aspire service discovery
- API → Database: Connection string managed by Aspire orchestration
- All services expose health endpoints via `MapDefaultEndpoints()`

#### Rationale

**Why Aspire?**
- Built-in orchestration eliminates docker-compose complexity
- Service discovery and configuration management out of the box
- Integrated observability (logging, metrics, tracing) from day one
- Local development experience matches cloud deployment

**Why This Structure?**
- **AppHost separation**: Keeps orchestration concerns isolated from business logic
- **ServiceDefaults**: DRY principle for Aspire configuration across services
- **Clear layers**: Frontend (Web) → API (Business Logic) → Database (Persistence)
- **Minimal coupling**: Each project has clear boundaries and responsibilities

**Why PostgreSQL?**
- Robust relational database with excellent .NET support
- PgAdmin provides GUI for development and debugging
- Aspire has first-class PostgreSQL integration

#### Consequences

##### Positive
- ✅ Rapid local development with full-stack orchestration
- ✅ Built-in telemetry and health checks from the start
- ✅ Clear separation of concerns between projects
- ✅ Easy to add additional services (Redis, message queues, etc.)
- ✅ Development experience is consistent with production

##### Considerations
- ⚠️ Aspire is relatively new; stay current with updates
- ⚠️ Team needs to understand Aspire orchestration model
- ⚠️ ServiceDefaults warning is expected (it's a library, not executable)

#### Alternatives Considered

1. **Docker Compose** - More manual configuration, less integrated tooling
2. **Separate Blazor + API solutions** - More complex to orchestrate locally
3. **SQL Server** - Chose PostgreSQL for cross-platform consistency

#### Implementation Notes

- .NET 10 SDK (10.0.201) with Aspire 13.2.2
- Aspire templates installed via `Aspire.ProjectTemplates` NuGet package
- Build succeeds with expected ASPIRE004 warning about ServiceDefaults
- Ready for feature development and database migrations

#### Next Steps

1. Add Entity Framework Core to the API project
2. Configure database migrations for PostgreSQL
3. Implement actual API endpoints beyond weather forecast
4. Wire up Blazor frontend to consume API
5. Add authentication and authorization

### Decision: User Management — Hybrid Keycloak + PostgreSQL Architecture

**Date:** 2026-04-11
**Author:** Data (Backend Dev)
**Status:** Active

## Context

Michael wants to manage users through our UI instead of Keycloak's admin console. The system needs two roles: System Administrator and User.

## Decision

### Hybrid Identity Architecture
- **Keycloak** remains the sole identity provider for authentication (login, passwords, OIDC tokens).
- **Our PostgreSQL database** stores application user profiles (`ApplicationUser`) with roles and metadata.
- **Our API** manages both systems: creating a user provisions them in Keycloak (via Admin REST API) AND stores their profile in our DB.
- On login, the middleware matches the Keycloak `sub` claim or email to the application user record and enriches the ClaimsPrincipal with the DB-backed role.

### Authorization Strategy
- "SystemAdministrator" policy checks both Keycloak realm roles (`system-administrator` in `realm_access`) and DB-backed `app_role` claim.
- Role enrichment happens via middleware in the request pipeline, after `UseAuthentication()` / `UseAuthorization()`.
- The `/api/users/me` endpoint auto-links the Keycloak subject ID to the DB user on first login.

### Keycloak Admin Access
- The API calls Keycloak Admin REST API using the default admin credentials.
- Admin credentials passed as configuration (`Keycloak:AdminUser`, `Keycloak:AdminPassword`).
- Token management with auto-refresh handled by `KeycloakAdminService`.

## Consequences
- ✅ Users managed through our UI — no need for Keycloak admin console access.
- ✅ Application roles decoupled from Keycloak realm roles — we control authorization logic.
- ✅ Keycloak subject ID linked on first login — smooth migration path.
- ⚠️ Two sources of truth for user data — must keep Keycloak and DB in sync via API.
- ⚠️ Admin credentials in config — must secure in production (use Aspire secrets or key vault).

### Decision: Multi-Tenancy Architecture

**Date:** 2026-04-11
**Author:** Data (Backend Dev)
**Status:** Active

## Context

Michael wants the app to support multiple companies (tenants) using a shared deployment. Users — particularly contractors — may belong to multiple tenants simultaneously. After login, users with multiple tenants select which one they're working in.

## Decision

### Shared Database, Row-Level Isolation

- Single PostgreSQL database with a `Tenants` table and a `UserTenantMemberships` join table.
- Tenant context is conveyed via an `X-Tenant-Id` HTTP header from the frontend on every request.
- Middleware validates that the authenticated user is a member of the specified tenant (or is a SystemAdministrator).
- `HttpContext.Items["TenantId"]` is set for downstream endpoint use.

### Global Roles, Not Per-Tenant Roles

- `UserRole` (User, SystemAdministrator) remains a global/platform concept on `ApplicationUser`.
- SystemAdministrators can access any tenant and manage all tenants/users.
- Regular users only see data within tenants they belong to.
- Per-tenant roles (e.g., TenantAdmin, TenantMember) are deferred to a future iteration if needed.

### Tenant Selection Flow

1. User logs in via Keycloak (unchanged).
2. Frontend calls `GET /api/users/me/tenants` to get the user's tenant list.
3. If multiple tenants, user picks one. If single tenant, auto-select.
4. Frontend stores TenantId and sends `X-Tenant-Id` header on all subsequent requests.
5. `POST /api/users/me/tenant` validates and confirms the selection.

### Migration Strategy

- Since the app hasn't shipped, we deleted the existing migration and created a fresh `InitialCreate` covering all tables (Users, Tenants, UserTenantMemberships).
- No migration chain complexity.

## Consequences

- ✅ Simple shared-DB model — no cross-database joins or connection switching.
- ✅ Contractors can naturally belong to multiple tenants.
- ✅ Existing auth, user management, and weatherforecast endpoints continue to work.
- ✅ Tenant header approach is stateless and works well with JWT tokens.
- ⚠️ No per-tenant roles yet — all authorization is global. May need expansion.
- ⚠️ Middleware performs DB queries per request for tenant validation — consider caching if this becomes a bottleneck.
- ⚠️ Future data entities that are tenant-scoped will need a `TenantId` FK column and query filters.

### Decision: Self-Service Profile Update API

**Date:** 2026-04-11
**Author:** Data (Backend Dev)
**Status:** Active

## Context

Users need to update their own name without requiring a SystemAdministrator to do it for them.

## Decision

Added `PUT /api/users/me` as a self-service profile endpoint. Only FirstName and LastName are editable — email and role changes remain admin-only via `PUT /api/users/{id}`. Changes are synced to Keycloak when the user has a linked subject ID.

## Consequences

- ✅ Users can manage their own profile without admin intervention
- ✅ Sensitive fields (email, role) remain protected behind admin authorization
- ✅ Keycloak stays in sync with local DB for name changes
- ✅ Frontend profile page and tests now complete

### Decision: User Management UI Architecture

**Date:** 2026-04-11
**Author:** Geordi (Frontend Dev)
**Status:** Active

## Context

Michael needs to manage users through the web UI instead of Keycloak's admin console.

## Decision

### API Client Pattern
- `UserApiClient` registered via `AddHttpClient<T>()` with Aspire service discovery for the API base URL
- `AuthTokenHandler` (delegating handler) forwards the user's OIDC access_token from `HttpContext` to the API as a Bearer token
- DTOs use string role names ("SystemAdministrator", "User") for simplicity

### User Management Page
- Route: `/users`, requires authentication via `[Authorize]` attribute
- Interactive Server render mode for real-time CRUD operations
- Inline add/edit form with Bootstrap styling, modal delete confirmation
- Uses `EditForm` with `DataAnnotationsValidator` for client-side validation

### Navigation
- "User Management" link added to NavMenu.razor, wrapped in `AuthorizeView` so only authenticated users see it

## Consequences
- ✅ Clean separation: API client handles HTTP + auth, page handles UI
- ✅ Consistent with existing app patterns (Bootstrap, AuthorizeView)
- ⚠️ Role-based nav filtering currently shows link to all authenticated users; full role-based restriction depends on Keycloak realm role claims being available
- ⚠️ API endpoints must exist and return matching DTOs for the UI to function

### Decision: Multi-Tenancy UI Architecture

**Date:** 2026-04-11
**Author:** Geordi (Frontend Dev)
**Status:** Active

#### Context

The app is becoming multi-tenant. Users (especially contractors) can belong to multiple tenants and need to select one after login. SystemAdministrators need full tenant CRUD + member management.

#### Decision

##### Tenant Selection via Cookie + In-Memory State

- After login, user lands on `/select-tenant` which shows their available tenants as clickable cards.
- On selection, a minimal API endpoint (`POST /select-tenant`) sets an HttpOnly cookie `X-Selected-Tenant={tenantId}`.
- `AuthTokenHandler` reads this cookie and forwards it as `X-Tenant-Id` header on every API request.
- `TenantStateService` (scoped) tracks the selected tenant in memory for Blazor component rendering (tenant name in top bar).
- This dual approach solves the DI lifetime mismatch: `AuthTokenHandler` is transient (can't inject scoped services), but can read cookies from `HttpContext`.

##### Login Flow Change

- `/login` redirect now points to `/select-tenant` instead of `/`.
- Single-tenant users auto-select and redirect to home transparently.
- Zero-tenant users see a "contact administrator" message.
- Logout clears the tenant cookie.

##### Tenant Management (SystemAdministrator)

- `/tenants` page with full CRUD — create, edit (name/slug/active toggle), delete with confirmation.
- Inline-expandable member panels per tenant — shows members, add from user dropdown, remove with confirmation.
- Follows existing `UserManagement.razor` Bootstrap styling patterns.

##### New Services

- `TenantApiClient` — typed HttpClient for all tenant API endpoints, same pattern as `UserApiClient`.
- `TenantStateService` — scoped service for in-memory tenant state with `OnTenantChanged` event.

#### Rationale

- Cookie-based tenant propagation avoids DI lifetime issues between `DelegatingHandler` (transient) and Blazor circuit state (scoped).
- Minimal API endpoints for cookie management are simple, secure (HttpOnly, SameSite=Strict), and don't need antiforgery.
- Card-based selector provides good UX for small tenant counts (typical for multi-tenant B2B).
- Auto-select for single-tenant users eliminates unnecessary friction.

#### Consequences

- ✅ Tenant context automatically included in all API calls via `AuthTokenHandler`
- ✅ Clean separation: cookie for HTTP pipeline, `TenantStateService` for Blazor rendering
- ✅ Login flow naturally guides users through tenant selection
- ⚠️ Cookie expires after 12 hours — user must re-select tenant after expiry
- ⚠️ `TenantStateService` in-memory state resets on circuit reconnect — falls back to cookie presence (shows "Selected Organization" instead of tenant name)
- ⚠️ API endpoints (`/api/tenants/*`, `/api/users/me/tenants`) being built by Data in parallel — pages will show errors until API is live

### Decision: Profile Page Pattern

**Date:** 2026-04-11
**Author:** Geordi (Frontend Dev)
**Status:** Active

## Context

Users need to view and edit their own profile information. A dedicated profile page provides a central place to manage display name.

## Decision

- Profile page at `/profile` (`Components/Pages/Profile.razor`) — requires auth, InteractiveServer render mode
- Uses `UserApiClient.GetCurrentUserAsync()` to load current user data
- Uses `UserApiClient.UpdateProfileAsync()` and `UpdateProfileRequest` for profile updates
- Email and Role are displayed as readonly/disabled fields (not editable by the user)
- First Name and Last Name are editable via `EditForm` with `OnValidSubmit`
- Follows the same card-based, centered layout pattern as `TenantSelector.razor`
- NavMenu username styled as a text link with hover-underline effect using `.profile-link` scoped style class
- NavMenu username now links to `/profile` for easy access

## Consequences

- ✅ Users can self-service edit their display name
- ✅ Consistent card-based UI pattern across pages
- ✅ Sensitive fields (email, role) protected from user editing
- ✅ Profile link in navbar provides easy navigation

### Decision: Playwright E2E Test Framework

**Date:** 2026-04-11
**Author:** Geordi (Frontend Dev)
**Status:** Active

## Context

The project needed end-to-end tests that exercise the full Blazor Server + Keycloak OIDC + multi-tenancy flow in a browser.

## Decision

- **Playwright for .NET** (Microsoft.Playwright.NUnit) — the official Playwright .NET integration uses NUnit, not xUnit
- Tests live in `WarpBusiness.Web.Tests/` and are added to the solution
- Tests run against a live Aspire environment — they are NOT self-contained
- `APP_URL` env var controls the target URL (defaults to `https://localhost:5001`)
- `PlaywrightTestBase` provides reusable `LoginAsync()` and `LoginAndSelectTenantAsync()` helpers
- Tests use CSS class selectors and text locators (no `data-testid` attributes on existing Blazor pages)

## Consequences

- ✅ Real browser testing catches OIDC/Blazor rendering issues that unit tests miss
- ✅ NUnit integration gives proper test isolation (new browser context per test)
- ⚠️ Tests require the full Aspire stack running (Keycloak, API, PostgreSQL)
- ⚠️ Playwright browsers must be installed after build (`pwsh playwright.ps1 install`)
- ⚠️ Consider adding `data-testid` attributes to Blazor components for more resilient selectors

### Decision: Test Infrastructure for WarpBusiness API

**Date:** 2026-04-11
**Author:** Worf (Tester)
**Status:** Active

## Context

The project needed a comprehensive test suite covering migrations, data layer, seeding, and API endpoints.

## Decision

### Test Stack

- **xUnit** for test framework (consistent with .NET ecosystem conventions)
- **FluentAssertions** for readable assertions
- **Testcontainers.PostgreSql** for real PostgreSQL integration tests (migration testing, FK constraints, unique indexes)
- **InMemory provider** for fast unit tests (DbContext CRUD, cascade deletes)
- **NSubstitute** available but **FakeHttpMessageHandler** preferred for KeycloakAdminService (non-virtual methods)

### Test Architecture

- **Shared PostgreSQL container** via `[Collection("Database")]` fixture — one container per test run, not per class
- **Endpoint methods tested via reflection** — private static methods invoked directly with controlled DbContext and mocked services, avoiding full WebApplicationFactory overhead
- **Status code assertions via reflection** for anonymous-typed results (`Results.Conflict(new { })`)

### Test Categories (56 tests total)

1. **Migration tests** (5): Apply, idempotency, pending check, table verification, schema match
2. **DbContext unit tests** (8): CRUD, composite keys, cascade deletes, role conversion
3. **DbInitializer tests** (4): Seed tenant, admin user, membership, idempotency
4. **Tenant endpoint tests** (15): Full CRUD, member management, access control, tenant selection
5. **User endpoint tests** (18): Current user, CRUD, Keycloak integration, cascade behavior, profile updates

## Consequences

- ✅ Migration testing against real PostgreSQL catches schema issues InMemory cannot
- ✅ Reflection-based endpoint testing is fast and avoids auth middleware complexity
- ✅ 56 tests comprehensive coverage with all passing
- ⚠️ If endpoint method signatures change, reflection calls must be updated manually
- ⚠️ Docker must be available for PostgreSQL container tests

### Decision: Blazor Server Token Forwarding Pattern

**Date:** 2026-04-11  
**Agent:** Coordinator  
**Status:** Inbox

## Context

In Blazor Server interactive mode, don't rely on `IHttpContextAccessor` in `DelegatingHandler` implementations for auth token forwarding. Instead, use a `CircuitHandler` to capture tokens when the circuit opens, store them in a scoped `TokenProvider`, and have typed HTTP clients set `DefaultRequestHeaders` from the `TokenProvider` in their constructors.

## Rationale

- **IHttpClientFactory handler pipeline** runs in a separate DI scope from the Blazor circuit
- **Scoped services** instantiated in different scopes are different instances — the handler's `TokenProvider` ≠ circuit's `TokenProvider`
- **HttpContext is null** after SignalR circuit initialization, breaking auth forwarding in the handler pipeline
- **Typed clients run in circuit scope**, allowing them to access the correct `TokenProvider` instance
- **Constructor-time header setup** ensures auth headers are present for all client requests without relying on per-request handler logic

## Pattern

1. Create scoped `TokenProvider` service to hold the access token
2. Create `CircuitHandler` subclass with `OnCircuitOpenedAsync` override
3. In `OnCircuitOpenedAsync`, retrieve token and set it in `TokenProvider`
4. In typed client constructors, read token from `TokenProvider` and set `DefaultRequestHeaders.Authorization`

## Key Files

- `TokenProvider.cs` — Scoped service storing access token
- `TokenCircuitHandler.cs` — CircuitHandler capturing token on circuit open
- Typed clients (`UserApiClient.cs`, `TenantApiClient.cs`) — Set auth headers in constructors

## Benefits

✅ Works reliably in Blazor Server InteractiveServer mode  
✅ No dependency on `IHttpContextAccessor` (which is null in circuit)  
✅ Token is captured once per circuit (no per-request overhead)  
✅ Auth headers present for all typed client requests  
✅ Reusable pattern for other interactive components

### Decision: Team Directive — Branch-Based Workflow

**Date:** 2026-04-11T15:46:08Z  
**By:** Michael R. Schmidt (via Copilot)  
**Status:** Active

Work in branches and pull requests — no direct commits to the main branch.

**Rationale:** User request — ensures code review and maintains clean git history.

### Decision: Team Directive — Multi-Schema Architecture

**Date:** 2026-04-12T04:33:22Z  
**By:** Michael R. Schmidt (via Copilot)  
**Status:** Active

Move all databases into a schema named "warp". System databases contain all common tables. Future modules will have new schemas with new data.

**Rationale:** User request — establishes the multi-schema architecture pattern for the project.

### Decision: Auth Token Flow Diagnostic Instrumentation

**Date:** 2026-04-11
**Author:** Data (Backend Dev)
**Status:** Active

## Context

The User Management page returns 401 Unauthorized at runtime despite the auth token persistence code (PersistentComponentState, per-request headers) compiling and tests passing. Root cause investigation required visibility into the runtime token flow.

## Decision

Added structured diagnostic logging at every stage of the authentication token lifecycle:

1. **Web side:** AuthTokenHandler, AuthenticatedComponentBase, TokenCircuitHandler, UserApiClient, TenantApiClient all log token presence/absence, phase (SSR vs circuit), and error conditions.
2. **API side:** JwtBearerEvents log token receipt, validation success/failure with specific error details (issuer, audience, signature), and challenge issuance.
3. **Startup logging:** Web project logs resolved Keycloak and API URLs to catch URL mismatches.

## Identified Likely Root Cause

Keycloak's `WithDataVolume()` preserves realm state across restarts. The `oidc-audience-mapper` for `warpbusiness-api` was added to `warpbusiness-realm.json` after the initial realm import. Keycloak skips import when the realm already exists, so the access token may lack the `warpbusiness-api` audience claim, causing the API's JWT audience validation to reject it.

## Action Required

Delete the Keycloak data volume and restart the Aspire AppHost to force a fresh realm import with the audience mapper. Then check structured logs for `[JWT]` prefixed messages to confirm token validation succeeds.

## Consequences

- ✅ Complete runtime visibility into auth failures
- ✅ JWT rejection reason immediately visible in Aspire dashboard logs
- ⚠️ Diagnostic logging should be removed or reduced to Debug level before production
- ⚠️ Keycloak data volume must be deleted when realm config changes

### Decision: Wire Keycloak Admin Password via Aspire ParameterResource

**Date:** 2026-04-12
**Author:** Data (Backend Dev)
**Status:** Active

## Context

The API project needs Keycloak admin credentials to call the Admin REST API (user CRUD). The AppHost was only passing the admin username, not the password. Since Aspire generates a random admin password for the Keycloak container, the API's fallback of "admin" caused 401 Unauthorized errors.

## Decision

Use `keycloak.Resource.AdminPasswordParameter` to pass the Aspire-generated password to the API project as an environment variable. This is the cleanest approach — no hardcoded passwords, no extra parameters, just reading what Aspire already generates.

```csharp
.WithEnvironment("Keycloak__AdminPassword", keycloak.Resource.AdminPasswordParameter)
```

## Consequences

- ✅ Admin password stays in sync automatically — no manual coordination needed.
- ✅ Password is treated as a secret parameter by Aspire (not logged in plaintext).
- ✅ Works with Keycloak data volumes — password is set at container creation, not on every restart.
- ⚠️ If someone adds a second project that needs admin access, they must also wire this parameter.

### Decision: Keycloak Realm Config — Logout, Role Name, and Claim Path Fixes

**Author:** Data (Backend Dev)
**Date:** 2026-04-12
**Status:** Implemented

## Context

Three issues in `warpbusiness-realm.json` prevented correct auth behavior at runtime:

1. **Logout broken:** `.NET`'s `SignOutAsync` sends `post_logout_redirect_uri` to Keycloak, but the client had no `post.logout.redirect.uris` attribute. Keycloak silently ignored the redirect, stranding users on the Keycloak logout page.   

2. **Role mismatch:** The realm defined the role as `system-administrator` (kebab-case) but the Blazor frontend checks `Roles="SystemAdministrator"` (PascalCase). Role-gated UI never appeared.

3. **Nested claim path:** The protocol mapper used `claim.name: "realm_access.roles"`, producing nested JSON in the token. .NET's OIDC middleware doesn't auto-flatten nested claims into `ClaimsPrincipal.IsInRole()` checks.

## Decision

- Added `"attributes": { "post.logout.redirect.uris": "+" }` to the `warpbusiness-web` client. The `+` value tells Keycloak to match against the existing `redirectUris` list.
- Renamed the realm role from `system-administrator` to `SystemAdministrator` in both the role definition and the user's `realmRoles` assignment.
- Changed `claim.name` from `realm_access.roles` to `roles` so roles appear as a flat top-level claim in the JWT.       

## Consequences

- Logout now correctly redirects back to the app.
- `<AuthorizeView Roles="SystemAdministrator">` and `[Authorize(Roles = "SystemAdministrator")]` will match the Keycloak-issued role claim.
- The Keycloak data volume must be deleted and the container restarted for these changes to take effect.

### Decision: Fix Keycloak User Update Error

**Author:** Data (Backend Dev)
**Date:** 2026-04-12
**Status:** Implemented
**PR:** #7 (merged)

## Context

When updating a user profile, the `UpdateUserAsync` method sent `username = email` in the PUT payload to Keycloak. The realm configuration has `editUsernameAllowed: false`, so Keycloak rejected the entire update with a `BadRequest` containing `error-user-attribute-read-only`. The error was logged but swallowed — neither calling endpoint checked the return value — so the UI showed "updated successfully" while the Keycloak update silently failed.

## Decision

1. **Removed `username` from the update payload.** The username is set at user creation time and matches the email. It is read-only in Keycloak and should never be sent in update requests.

2. **Propagated Keycloak failures to callers.** Both `UpdateMyProfile` and admin `UpdateUser` endpoints now check the boolean return value from `UpdateUserAsync`. On failure, they return `Results.Problem("Failed to update user in identity provider.")` instead of silently proceeding.

## Consequences

- Profile and admin user updates now correctly succeed in Keycloak.
- If Keycloak is down or rejects an update for any reason, the API returns a clear error instead of falsely reporting success.
- No DB changes are made if the Keycloak update fails (fail-fast before persisting).

### Decision: Automatic Access Token Refresh Pattern

**Author:** Data
**Date:** 2026-04-11
**Branch:** fix/blazor-auth-token-persistence

## Problem

OIDC `SaveTokens = true` stores the access token in the auth cookie at login. After the token expires (typically 5–15 minutes for Keycloak), every API call returns `401 invalid_token`. The user's session in Keycloak is still valid (refresh token is long-lived), but the app never exchanges it for a fresh access token.

## Decision

Implement a **two-layer automatic refresh** strategy in `WarpBusiness.Web`:

### Layer 1 — Proactive (AuthenticatedComponentBase)
During the SSR prerender phase, decode the JWT `exp` claim and check if the token expires within 60 seconds. If so, call `TokenRefreshService` before caching the token in `TokenProvider` and before it is transferred to the circuit. Update the auth cookie via `HttpContext.SignInAsync` with the new tokens.

### Layer 2 — Reactive (AuthTokenHandler)
When any API call returns `401` with `WWW-Authenticate: Bearer error="invalid_token"`, immediately attempt a refresh using the available refresh token (`HttpContext` in SSR, `TokenProvider.RefreshToken` in circuit). On success, update the token store, update the cookie (SSR only), and retry the original request exactly once.

## Implementation

- **`TokenRefreshService`** (new, transient): calls `POST {keycloakUrl}/realms/warpbusiness/protocol/openid-connect/token` with `grant_type=refresh_token`. Uses the dedicated `"keycloak-token"` named `HttpClient`.
- **`TokenProvider`**: added `RefreshToken` property alongside `AccessToken`.
- **Named HttpClient `"keycloak-token"`**: registered in `Program.cs` with dev-mode TLS bypass. Isolated from the API client pipeline to prevent circular dependencies.
- **Request buffering**: `AuthTokenHandler` calls `LoadIntoBufferAsync` on the request content before the first send, enabling replay on retry.
- **Cookie update pattern**: `httpContext.AuthenticateAsync(Cookies)` → `properties.UpdateTokenValue(...)` → `httpContext.SignInAsync(Cookies, principal, properties)`.
- **Circuit phase limitation**: Cannot write HTTP cookies over SignalR. In circuit phase, `TokenProvider` is updated in-memory for the lifetime of the circuit. On next full page load, the cookie will contain fresh tokens from Keycloak.     

## Alternatives Considered

- **Middleware refresh**: Refresh on every request before forwarding. Rejected — unnecessary overhead; most requests use valid tokens.
- **Background timer**: Refresh on a schedule. Rejected — adds complexity; reactive + proactive is sufficient and simpler.
- **Force re-login on 401**: Redirect to `/login`. Rejected — poor UX; Keycloak session is still valid.

## Impact

- All team members should store and forward both `access_token` and `refresh_token` when building new token-aware services.
- The `"keycloak-token"` HttpClient must not have `AuthTokenHandler` in its pipeline.
- `PersistedTokenData` now has three fields: `AccessToken`, `RefreshToken`, `SelectedTenantId`.

### Decision: Keycloak Error Response Handling Pattern

**Date:** 2026-04-12
**Author:** Data (Backend Dev)
**Status:** Proposed

## Context

User creation was returning 500 errors because Keycloak 400-level responses (password policy violations, duplicate users) were treated as opaque failures. The API returned 502, Aspire's retry policy retried it 3 times, and the whole thing surfaced as a 500.

## Decision

- `KeycloakAdminService` methods that call Keycloak Admin API now return `KeycloakOperationResult` (typed result with status code + parsed error message) instead of `null` on failure.
- API endpoints map Keycloak status codes to appropriate HTTP responses: 409→Conflict, 4xx→400 with detail message, 5xx→502.
- Keycloak JSON error responses are parsed to extract human-readable messages (`errorMessage`, `error_description`, `error` fields).
- This pattern should be applied to any future Keycloak Admin API integrations (e.g., password reset, role assignment). 

## Impact

- **Frontend (Geordi):** API now returns 400 with `detail` field for validation errors instead of 500. Error messages from Keycloak are passed through.
- **Backend:** Other `KeycloakAdminService` methods (`SetPasswordAsync`, `DeleteUserAsync`, `UpdateUserAsync`) still return `bool` — these should be migrated to `KeycloakOperationResult` if richer error handling is needed.

### Decision: Database Schema Namespacing with PostgreSQL Schemas

**Date:** 2026-04-12
**Author:** Data (Backend Dev)
**Status:** Proposed
**PR:** #10

## Context

All database tables were in the default `public` PostgreSQL schema. As the application grows and new modules are added, we need a way to organize tables by domain and prevent naming collisions.

## Decision

### Use PostgreSQL schemas for namespace separation

- The `warp` schema holds all system/common tables (Users, Tenants, UserTenantMemberships).
- `WarpBusinessDbContext` uses `modelBuilder.HasDefaultSchema("warp")` so all tables are automatically placed in the `warp` schema.
- Future modules (e.g., billing, inventory) will get their own schemas with their own DbContext instances.

### Why PostgreSQL schemas

- PostgreSQL schemas are a first-class concept — lightweight, zero-overhead namespace separation.
- EF Core has native support via `HasDefaultSchema()` and per-entity `.ToTable("name", "schema")`.
- No changes needed to application code — EF Core qualifies all SQL automatically.
- Existing data is preserved during migration (PostgreSQL `ALTER TABLE ... SET SCHEMA` moves tables without copying data).

## Consequences

- All new tables added to `WarpBusinessDbContext` will automatically go into the `warp` schema.
- New module DbContexts should declare their own schema via `HasDefaultSchema()`.
- Cross-schema queries work naturally in PostgreSQL (just qualify with `schema.table`).
- Database tooling (pgAdmin, psql) will show tables organized by schema.

## Alternatives Considered

- **Table name prefixes** (e.g., `warp_users`): Clutters entity names, no tooling support, not reversible.
- **Separate databases per module**: Too heavy for current scale, complicates cross-module queries.

### Decision: Client-Side Password Policy Validation

**Date:** 2026-04-12
**Author:** Geordi (Frontend Dev)
**Status:** Active

## Context

Users were getting 500 errors when creating users because Keycloak enforces password policies server-side. The frontend had no password validation, and the API was not returning helpful error messages.

## Decision

- **Password policy is enforced client-side** in the user creation form with inline validation (8+ chars, uppercase, lowercase, digit, special character). This mirrors the Keycloak realm policy.
- **Submit button is disabled** until all password requirements are met.
- **ApiException** class introduced in `UserApiClient.cs` to parse JSON error bodies from API responses and surface user-friendly messages. It checks for `message`, `detail`, and `title` fields in JSON responses.
- **Error handling covers both current 500s and future 400s** — the frontend gracefully handles any non-success status code from the API.

## Consequences

- ✅ Users get immediate feedback on password requirements before submission.
- ✅ API errors (400, 409, 500) are displayed as readable messages, not raw exceptions.
- ⚠️ If the Keycloak password policy changes, the client-side rules must be updated to match. Consider a future `/api/password-policy` endpoint to fetch rules dynamically.
- ⚠️ Other API write methods (`UpdateUserAsync`, `DeleteUserAsync`) still use `EnsureSuccessStatusCode` — should be migrated to `ApiException` pattern if similar issues arise.

### Decision: Set RoleClaimType to "roles" in OIDC config

**Date:** 2026-04-11
**Author:** Geordi (Frontend Dev)
**Status:** Implemented

## Context
The `AuthorizeView Roles="SystemAdministrator"` directive in NavMenu.razor (and potentially other components) was not working because .NET didn't know which token claim contained role information. Keycloak's default `realm_access.roles` nested claim isn't directly readable by .NET's built-in role machinery.

## Decision
Added `options.TokenValidationParameters.RoleClaimType = "roles";` to the OIDC configuration in `WarpBusiness.Web/Program.cs`. This tells .NET to read the `roles` claim from the token as the source of role information.

## Coordination
Data is updating the Keycloak client mapper to flatten `realm_access.roles` into a top-level `roles` claim, and standardizing role names (e.g., `SystemAdministrator`). This frontend change is the matching piece — .NET now reads `roles` as the role claim type.

## Impact
- `AuthorizeView Roles="SystemAdministrator"` in NavMenu.razor will now correctly gate the Tenant Management link       
- Any future `AuthorizeView Roles="..."` usage will work without additional config
- `HttpContext.User.IsInRole("...")` calls in backend-for-frontend code will also work

### Decision: Employee Module Architecture

**Date:** 2026-04-12
**Author:** Data (Backend Dev)
**Status:** Active

## Context

The project needed an Employee/HR data module. The existing architecture uses schema-per-module isolation in a shared PostgreSQL database.

## Decision

### Separate Class Library

Created `WarpBusiness.Employees` as a standalone .NET class library (not embedded in the API project). This keeps employee concerns isolated and makes the module portable.

### Schema Isolation

`EmployeeDbContext` uses `HasDefaultSchema("employees")` — completely separate from the `warp` schema. Both contexts share the same `warpdb` connection string. The `EmployeeDbInitializer` runs its own migrations independently.

### Tenant Scoping Without Cross-Schema FKs

Employee records reference `TenantId` (Guid) but there is no cross-schema foreign key to `warp.Tenants`. This is intentional — it keeps the modules decoupled at the database level. Tenant validation happens at the API layer via the existing tenant middleware.

### Employee Number Generation

Auto-generated sequential numbers per tenant (EMP00001 format) using MAX query. Simple and sufficient for current scale. If high-concurrency inserts become a concern, this could move to a database sequence per tenant.

## Consequences

- ✅ Clean module boundaries — Employee code is self-contained in its own project
- ✅ Independent migrations — Employee schema evolves independently of warp schema
- ✅ No cross-schema FK coupling — modules can be extracted to separate databases later if needed
- ⚠️ No referential integrity between employees.Employees.TenantId and warp.Tenants — enforced at API layer only
- ⚠️ Employee number generation has a race condition under concurrent inserts (acceptable for current scale)

### Decision: EmployeeNumber Unique Constraint Scoped to Tenant

**Date:** 2026-04-13
**Author:** Data (Backend Dev)
**Status:** Active

## Context

The `EmployeeNumber` column had a global unique index (`IX_Employees_EmployeeNumber`). Since employee numbers are generated per-tenant (EMP00001, EMP00002, …), this prevented different tenants from ever having the same employee number — a cross-tenant collision.

## Decision

Changed the unique index to a composite on `(EmployeeNumber, TenantId)` so uniqueness is enforced within each tenant independently.

## Implications

- **Multi-tenant pattern:** All unique business identifiers in this system should include `TenantId` in their uniqueness constraint. The `Email` unique index may need the same treatment if email addresses are meant to be unique per-tenant rather than globally.
- **Migration:** `FixEmployeeNumberTenantScopedUnique` must be applied to existing databases. It is safe to apply — it relaxes the constraint (allows more rows, not fewer).
- **EF tooling:** When running `dotnet ef` commands, always specify `--context EmployeeDbContext` since multiple DbContexts exist in the solution.

## PR

- #19

### Decision: Employee-User Linking Backend Architecture

**Author:** Data (Backend Dev)
**Date:** 2026-04-13
**Status:** Implemented

## Context
Employee-User account linking requires endpoints that access both `WarpBusinessDbContext` (users, tenants) and `EmployeeDbContext` (employees). These live in separate projects.

## Decisions

1. **New `EmployeeUserEndpoints.cs` in `WarpBusiness.Api`** — Combined endpoints that need both DbContexts are placed in the API project where both are registered. The existing `EmployeeEndpoints.cs` in the Employees project remains unchanged for basic CRUD.

2. **Passwordless user creation via Keycloak `requiredActions`** — Instead of generating temporary passwords, we set `requiredActions = ["UPDATE_PASSWORD"]` and send a Keycloak "execute actions email" for the user to set their own password. This is more secure and avoids password transmission.

3. **Best-effort email sending** — `SendRequiredActionsEmailAsync` failures are logged but never throw. The user can still be created; an admin can manually trigger password reset later.

4. **Filtered unique index on UserId** — `WHERE "UserId" IS NOT NULL` allows multiple null values while enforcing one-user-one-employee for linked records.

5. **Tenant-scoped email uniqueness** — Changed from global email uniqueness to `(Email, TenantId)` composite unique index, allowing the same email across different tenants.

6. **LinkedEmployeeId in user responses** — Batch-queried from EmployeeDbContext to avoid N+1 queries. Added as optional parameter to existing response records.

### Decision: Use Absolute URLs for OIDC Post-Logout Redirects

**Author:** Data (Backend Dev)
**Date:** 2026-04-13
**Status:** Implemented

## Context

After logout, users were being redirected to the Keycloak login page instead of the Warp web app's home page. The `PostLogoutRedirectUri` was set to `"/"` in two places in `WarpBusiness.Web/Program.cs`.

## Decision

Build absolute redirect URLs dynamically from `HttpContext.Request` (`{scheme}://{host}/`) instead of using relative paths. This applies to:

1. `OnRedirectToIdentityProviderForSignOut` — the OIDC protocol message sent to Keycloak
2. `/logout` endpoint — the `AuthenticationProperties.RedirectUri` for cookie sign-out

## Rationale

Keycloak (and most OIDC providers) interpret relative paths as relative to themselves, not the requesting application. Since Aspire assigns dynamic ports, hardcoding URLs isn't viable — `Request.Scheme` + `Request.Host` gives us the correct origin every time.

## Team Impact

- **Geordi:** No frontend changes needed. Logout navigation (`/logout`) works as before.
- **Worf:** Existing logout tests should still pass. If testing redirect URLs, expect absolute URLs now.

### Decision: Adopt Orphaned Keycloak Users on Creation

**Date:** 2026-04-12
**Author:** Data (Backend Dev)
**PR:** #14

## Context

When a user exists in Keycloak but not in our warp.Users table (e.g., from a previous partial creation that failed after the Keycloak call but before the DB insert), the CreateUser endpoint returned 409 Conflict and the user was stuck — couldn't be added because Keycloak blocked it, but didn't exist locally either.

## Decision

On Keycloak 409, the CreateUser endpoint now attempts to **adopt** the orphaned Keycloak user rather than failing:

1. Look up the existing Keycloak user by email
2. Check if they exist in local DB (by KeycloakSubjectId or email)
3. If missing locally → create the local record linking to the existing Keycloak ID
4. If present locally → return the genuine duplicate error

## Rationale

- Avoids requiring manual Keycloak admin intervention to delete and re-create users
- The orphan scenario is a known failure mode in distributed systems with two-phase writes (Keycloak + PostgreSQL)
- The adopt pattern is safe because we verify the email matches and the user truly doesn't exist locally

## Impact

- Backend only — no frontend changes needed
- Existing CreateUser behavior unchanged for non-conflict cases
- Tests updated to pass new logger parameter

### Decision: Warp Brand Rebrand Strategy

**Author:** Geordi (Frontend Dev)
**Date:** 2026-04-13
**Status:** Implemented

## Context

The Blazor app used stock Bootstrap with generic colors and "Hello, world!" placeholder content. The marketing site has a polished dark space theme with a defined palette, Orbitron/Inter fonts, and SVG branding.

## Decision

Override Bootstrap via CSS custom properties rather than replacing it. This preserves Bootstrap's grid, components, and JS functionality while applying the Warp palette on top. No C# code was changed — purely visual.

## Key Design Tokens

All Warp colors are defined as CSS custom properties in `app.css` (`:root` block). Any new components should use these variables (e.g., `var(--clr-bg-card)`, `var(--clr-accent)`) rather than hardcoding hex values.

## Impact

- **All team members:** New components and pages should use the Warp palette variables, not Bootstrap's default colors.
- **Worf (Testing):** Home.razor content changed — any E2E tests referencing "Hello, world!" need updating.
- **Future work:** Consider extracting shared brand assets (logo SVG, color tokens) into a shared project if the marketing site and Blazor app diverge.

### Decision: Employee Management UI Pattern

**Date:** 2026-04-12
**Author:** Geordi (Frontend Dev)
**Status:** Active

## Context

Added the first "module" page (Employee Management) to the application, establishing the pattern for future business-domain pages under the Modules nav dropdown.

## Decision

- Module pages go under a "Modules" dropdown in NavMenu (Bootstrap dropdown-menu-dark, wrapped in AuthorizeView)
- Each module gets its own ApiClient following the same TokenProvider/CreateRequest pattern as UserApiClient and TenantApiClient
- API enum values (e.g., EmploymentStatus, EmploymentType) are treated as strings in Web DTOs — JSON serialization handles the conversion
- CRUD pages inherit from AuthenticatedComponentBase and follow the same card-form + table + delete-modal layout as UserManagement.razor
- Status fields use color-coded badges for visual distinction

## Consequences

- ✅ Consistent UX pattern for all future module pages
- ✅ Modules dropdown is extensible — just add new `<li>` items
- ⚠️ Future modules should follow this same pattern to maintain consistency

### Decision: Employee-User Linking Frontend Architecture

**Date:** 2026-04-12
**Author:** Geordi (Frontend Dev)
**Status:** Active

## Context

Employee and User accounts need to be linkable. The frontend must support creating employees with or without user accounts, linking to existing users, and combined editing of linked records.

## Decision

### Combined Edit via URL Deep Link

- Employee Management page accepts `?edit={employeeId}` query parameter to auto-open the edit form
- User Management redirects to `/employees?edit={LinkedEmployeeId}` when editing a linked user
- This avoids duplicating edit forms across pages — Employee Management is the single source of truth for linked records

### Mutual Exclusion UX for User Account Creation

- "Link to Existing User" dropdown and "Create New User Account" section are mutually exclusive
- Selecting an existing user disables and collapses the create section
- Expanding create new user clears the existing user dropdown
- This prevents conflicting state and makes the user's intent clear

### API Contract for Parallel Development

- Frontend calls 4 new endpoints (`GET api/users/unlinked`, `POST api/employees/with-user`, `PUT api/employees/{id}/with-user`, `GET api/employees/by-user/{userId}`)
- DTOs: `CreateEmployeeWithUserRequest` (includes Role), `UpdateEmployeeWithUserRequest` (includes optional Role)
- Backend (Data) is building these endpoints in parallel on the same branch

## Consequences

- ✅ Single combined edit form avoids data sync issues between Employee and User pages
- ✅ URL deep-linking enables cross-page navigation without state sharing
- ✅ Mutual exclusion prevents ambiguous form states
- ⚠️ Frontend will show API errors until backend endpoints are deployed

### Decision: Employee EmployeeNumber Global Unique Constraint

**Author:** Worf (Tester)
**Date:** 2026-04-13
**Status:** Observation / Potential Issue

## Context

While writing employee endpoint tests, discovered that `EmployeeDbContext` defines `EmployeeNumber` with a **global unique index** (`entity.HasIndex(e => e.EmployeeNumber).IsUnique()`), but `GenerateEmployeeNumber` generates numbers scoped to a tenant (`WHERE TenantId == tenantId`).

This means two tenants will independently generate `EMP00001`, which violates the global unique constraint on second insert.

## Impact

- Multi-tenant employee creation will fail when two tenants create their first employee
- Tests had to work around this by directly inserting with distinct employee numbers

## Recommendation

Either:
1. Make the unique index composite: `(TenantId, EmployeeNumber)` instead of just `EmployeeNumber`, OR
2. Make `GenerateEmployeeNumber` query all tenants (remove the tenant filter)

Option 1 is preferred — employee numbers should be tenant-scoped conceptually.

## Files

- `WarpBusiness.Employees/Data/EmployeeDbContext.cs` (line 25)
- `WarpBusiness.Employees/Endpoints/EmployeeEndpoints.cs` (lines 204-219)

### Decision: Employee-User Linking Test Contracts

**Date:** 2026-04-13
**Author:** Worf (Tester)
**Status:** Pending Review

## Context

Written 22 unit tests + 4 E2E tests for the employee-user linking feature. Data and Geordi are building the backend and frontend in parallel.

## Key Decisions & Contracts

### 1. New endpoint method names (for Data)

The tests call these methods via reflection. Data should use these exact names in the endpoint classes:

- `UserEndpoints.GetUnlinkedUsers` — expected signature: `(HttpContext, WarpBusinessDbContext, EmployeeDbContext, CancellationToken)`
- `EmployeeEndpoints.CreateEmployeeWithUser` — expected to accept a request DTO + `(HttpContext, EmployeeDbContext, WarpBusinessDbContext, KeycloakAdminService, CancellationToken)`
- `EmployeeEndpoints.UpdateEmployeeWithUser` — expected: `(Guid id, request, HttpContext, EmployeeDbContext, WarpBusinessDbContext, KeycloakAdminService, CancellationToken)`
- `EmployeeEndpoints.GetEmployeeByUserId` — expected: `(Guid userId, HttpContext, EmployeeDbContext, CancellationToken)`

If Data uses different names or signatures, the reflection helpers in the test file have a `BuildEndpointArgs` dynamic matcher but the method names must match.

### 2. Cross-schema dependency for validation

`GetUnlinkedUsers` needs access to BOTH `WarpBusinessDbContext` (to find users in tenant) and `EmployeeDbContext` (to check which are linked). Data needs to inject both DbContexts into this endpoint.

Similarly, `DeleteUser` blocking requires checking the employee schema. Data needs to inject `EmployeeDbContext` into the user deletion flow.

### 3. Deletion blocking response code

Tests expect HTTP 400 (Bad Request) when attempting to delete a linked employee or user. If Data prefers 409 (Conflict), the tests need updating.

### 4. E2E selectors (for Geordi)

The E2E tests look for these UI elements:
- `text=User Account` — section heading in employee form
- `[data-testid='expand-create-user']` or `.user-account-expand` — chevron/expand button
- `th:has-text('User')` or `.bi-person-check` or `[data-testid='user-link-indicator']` — table link indicator
- Linked user edit button should redirect URL to contain `/employees/`

Geordi can use any of these selectors — tests check multiple patterns.

## Action Items

- **Data**: Verify method names match or inform Worf of differences
- **Geordi**: Add `data-testid` attributes for testability
- **Worf**: Adjust tests once both implementations land

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
