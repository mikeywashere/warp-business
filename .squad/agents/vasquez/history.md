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
