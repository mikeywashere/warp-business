# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, PostgreSQL, Blazor
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- OIDC auth configured in `WarpBusiness.Web/Program.cs` — cookie + OpenIdConnect scheme against Keycloak realm `warpbusiness`, client `warpbusiness-web`
- Keycloak URL resolved from Aspire service discovery config: `services:keycloak:https:0` or `services:keycloak:http:0`
- Login/logout are minimal API endpoints (`/login`, `/logout`), not Razor pages
- `AuthorizeView` used in both `Home.razor` and `MainLayout.razor` (top-row) for auth state display
- `CascadingAuthenticationState` registered via DI (`AddCascadingAuthenticationState()`) — modern Blazor approach, no wrapper component needed
- No official Aspire Keycloak client auth NuGet exists; used standard `Microsoft.AspNetCore.Authentication.OpenIdConnect` v10.0.5
- `preferred_username` is the Keycloak claim for display name — set via `TokenValidationParameters.NameClaimType`
- User Management page at `/users` (`Components/Pages/UserManagement.razor`) — requires auth, uses `InteractiveServer` render mode
- `UserApiClient` service (`Services/UserApiClient.cs`) wraps HTTP calls to the API for CRUD user operations
- `AuthTokenHandler` (`Services/AuthTokenHandler.cs`) — delegating handler that forwards the OIDC access_token from HttpContext to the API via Bearer header
- API base URL resolved from Aspire service discovery: `services:api:https:0` or `services:api:http:0`
- When nesting `EditForm` inside `AuthorizeView`, rename the `Authorized` context parameter to avoid Blazor `context` name collision (e.g. `<Authorized Context="authContext">`)
- NavMenu.razor uses `AuthorizeView` to conditionally show the User Management link for authenticated users
- DTOs for user API: `UserResponse`, `CreateUserRequest`, `UpdateUserRequest` — use string role names ("SystemAdministrator", "User")
- Multi-tenancy UI: tenant selection flow via cookie (`X-Selected-Tenant`) set by `POST /select-tenant` endpoint, read by `AuthTokenHandler` as `X-Tenant-Id` header on API calls
- `TenantStateService` (scoped) holds in-memory tenant state for Blazor components; cookie is the source of truth for `AuthTokenHandler` (transient DelegatingHandler)
- `TenantApiClient` mirrors `UserApiClient` pattern — typed HttpClient with `AuthTokenHandler`, registered in `Program.cs`
- Login redirect now goes to `/select-tenant` instead of `/`; single-tenant users auto-redirect to home
- `TenantSelector.razor` at `/select-tenant` — card-based layout, auto-selects for single tenant, SystemAdministrator gets "Manage All Tenants" link
- `TenantManagement.razor` at `/tenants` — SystemAdministrator only, inline-expandable member panels, add/remove members from user dropdown
- `MainLayout.razor` shows tenant indicator ("Working in: TenantName [Switch]") in top bar using `TenantStateService` with cookie fallback
- `NavMenu.razor` has "Tenant Management" link gated by `AuthorizeView Roles="SystemAdministrator"`
- Logout clears the `X-Selected-Tenant` cookie
- Playwright E2E test project at `WarpBusiness.Web.Tests/` — uses NUnit (Playwright .NET requires NUnit, not xUnit)
- Test base class `PlaywrightTestBase` extends `PageTest`, provides `LoginAsync()` and `LoginAndSelectTenantAsync()` helpers
- Keycloak OIDC test flow: navigate to `/login` → wait for `realms/warpbusiness` URL → fill `#username`/`#password` → click `#kc-login` → wait for `/select-tenant` redirect
- Tenant selector cards use `.tenant-card` class, card titles in `.card-title`, slugs in `small.text-muted`
- User table columns: Name, Email, Role, Created, Actions — no `data-testid` attributes, tests use CSS/text locators
- Tests require live Aspire environment (web + API + PostgreSQL + Keycloak); `APP_URL` env var or defaults to `https://localhost:5001`
- Playwright browser install: `pwsh WarpBusiness.Web.Tests/bin/Debug/net10.0/playwright.ps1 install`
- Profile page at `/profile` (`Components/Pages/Profile.razor`) — requires auth, InteractiveServer render mode, uses `UserApiClient.GetCurrentUserAsync()` and `UpdateProfileAsync()`
- NavMenu username is now a clickable link to `/profile` with hover-underline style (`.profile-link` class)
- Profile page follows card-based layout pattern (consistent with TenantSelector.razor)
- Email and Role displayed as readonly fields, FirstName/LastName editable via EditForm
- Comprehensive testing pending for profile form interactions via Playwright E2E
