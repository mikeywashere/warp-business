# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** Warp Business — Business Management System (CRM first)
- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core, Auth/Authz
- **Role:** Frontend Dev — Blazor components, UI, routing, state management
- **Created:** 2026-03-25

## Learnings

### 2026-03-25: Initial Blazor CRM Pages Built

**Components Created:**
- `WarpApiClient` service: HTTP client wrapper for API calls using typed HttpClient with service discovery via `https+http://api`
- `AuthStateService`: Singleton for managing auth state across components with event-based change notifications
- `ContactList.razor`: Paginated contact list with search, Bootstrap styling, inline status badges
- `Login.razor`: Form-based login with EditForm validation and error handling
- `Home.razor`: Conditional rendering based on auth state (dashboard vs. landing page)

**Routing & Navigation:**
- Updated NavMenu with CRM-specific links: Contacts, Companies, Deals, Activities
- Used NavLink for active state highlighting

**API Client Pattern:**
- Injected HttpClient configured with Aspire service discovery (`https+http://api`)
- Registered as scoped service via `AddHttpClient<WarpApiClient>`
- Simple fire-and-forget approach for now; error handling at component level

**Blazor Gotcha:** The `@page` variable name inside button text triggers Razor directive parsing. Fixed by using `pageNum`/`pageText` instead of `page`.

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-25: Provider-Aware Login, Register Page, NavMenu Auth State

**Provider-Aware Login Pattern:**
- `Login.razor` now calls `GET /api/auth/provider` on init via `Api.GetAuthProviderAsync()`
- Renders a spinner during discovery; falls back to local login on error or null response
- Three provider branches: `SupportsLocalLogin` → email/password form; `Keycloak` → redirect button using `keycloakAuthUrl`; `Microsoft` → placeholder button with guidance message (full OIDC flow TBD)
- `AuthProviderInfo` record is in `WarpBusiness.Shared.Auth`

**Register Page (`Register.razor`):**
- Route: `/register`; linked from Login page
- Collects FirstName, LastName, Email, Password with DataAnnotations validation
- Calls `Api.RegisterAsync()` and sets auth state on success, redirects to `/contacts`

**NavMenu Auth State:**
- NavMenu now `@implements IDisposable` and injects `AuthStateService` + `NavigationManager`
- Subscribes to `AuthState.OnChange` in `OnInitialized`, unsubscribes in `Dispose`
- Bottom of nav shows user's full name + Sign Out button when authenticated, Sign In link when not

