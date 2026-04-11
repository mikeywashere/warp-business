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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
