# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, Entity Framework Core, PostgreSQL
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

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
