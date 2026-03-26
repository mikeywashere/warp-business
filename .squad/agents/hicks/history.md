# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** Warp Business — Business Management System (CRM first)
- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core, Auth/Authz
- **Role:** Backend Dev — APIs, services, EF Core, domain logic
- **Created:** 2026-03-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-26: Security Fixes — Code Review Remediation

- **appsettings.json DefaultConnection:** Added a placeholder `DefaultConnection` key with `Password=CHANGE_ME` so the config schema is visible and no real credentials can be committed. Real creds must be supplied via env var (`ConnectionStrings__DefaultConnection`) or Aspire secrets.
- **IDOR pattern in controllers:** When a PUT endpoint operates on a resource that could be owned by a user (contact, deal, etc.), check ownership before delegating to the service. Pattern: load the resource via service, compare email/ownerId against JWT claim, return `Forbid()` for non-Admin/Manager users on mismatch. Service layer is not the right place for authz checks — controller is.
- **Email normalization:** Emails must be normalized to `ToLowerInvariant()` on both create AND update — in the service layer, not in the controller. `GetContactByEmailAsync` already searches by lowercase; the fix closes the round-trip gap.
- **Employee.Email is non-nullable:** `Employee.Email` defaults to `string.Empty`. Use `?.ToLowerInvariant() ?? string.Empty` in `UpdateAsync` to avoid `CS8601` null-assignment warnings.
- **Data annotation attributes on records:** C# positional record constructors support `[Required]`, `[MaxLength]`, `[EmailAddress]`, `[Phone]`, `[MinLength]` directly on the parameter. Add `using System.ComponentModel.DataAnnotations;` at top of file. ASP.NET Core model validation runs automatically with `AddControllers()` — no extra setup needed.
- **Shared git environment:** In a squad environment multiple agents share one working tree. Always check `git status` before `git add -A` — other agents may have staged changes. Use targeted `git add <file>` or `git checkout <commit> -- <file>` patterns to avoid mixing commits.

### 2026-03-26: Authorization Gaps Found in CRM Delete Endpoints (Hudson Finding)

- **Critical discovery:** CRM delete endpoints (Companies, Deals, Activities, Contacts) lack `[Authorize(Roles = "Admin")]`. Only class-level `[Authorize]` present — any authenticated user can delete.
- **Contrast:** EmployeesController correctly applies role restriction on Delete action.
- **Impact:** IDOR fix addressed ownership checks, but role-based authorization for destructive operations should be audited across all CRM controllers.
- **Next step:** Add `[Authorize(Roles = "Admin")]` to CompaniesController.Delete, DealsController.Delete, ActivitiesController.Delete, and review ContactsController.Delete scope (may need dual-level: Admin can delete any, Manager/Customer can only delete owned contacts, others forbidden).

### 2026-03-25: CRM Domain Model Implemented

- **Domain Entities:** Created Contact, Company, Deal, and Activity entities with proper relationships
  - Contact → Company (many-to-one, nullable)
  - Contact ↔ Deal (many-to-many via navigation properties)
  - Contact ↔ Activity (one-to-many)
  - Company ↔ Deal (one-to-many)
  - Deal ↔ Activity (one-to-many)
- **EF Core Configurations:** Built IEntityTypeConfiguration classes for all CRM entities with explicit column constraints, indexes, and delete behaviors (SetNull for soft relationships)
- **Service Layer Pattern:** Implemented thin controllers with full business logic in ContactService. All data access is async with CancellationToken support.
- **DTOs:** Created record-based DTOs in WarpBusiness.Shared.Crm for clean API contracts. PagedResult<T> pattern established for list endpoints with built-in pagination metadata.
- **ApplicationDbContext:** Successfully integrated CRM DbSets into Bishop's ApplicationDbContext. Used ApplyConfigurationsFromAssembly for automatic configuration discovery.
- **Standards:**
  - All service methods are async and accept CancellationToken
  - Query operations use AsNoTracking() for performance
  - CreatedBy and OwnerId fields link to ApplicationUser.Id (string, max 450 chars for EF Identity compatibility)
  - DateTimeOffset used consistently for temporal data

### 2026-03-25: Companies and Deals Services Implemented

- **Companies service pattern:** `ICompanyService` / `CompanyService` mirrors Contacts exactly. `DeleteCompanyAsync` returns a `DeleteCompanyResult` enum (`Deleted`, `NotFound`, `HasContacts`) to give the controller enough signal for 204/404/409 without leaking business logic into the HTTP layer.
- **409 Conflict handling:** `DeleteCompanyAsync` loads the company with `.Include(c => c.Contacts)` and short-circuits with `HasContacts` if any exist — contacts are never orphaned. The controller maps this to `Conflict(new { message = ... })`.
- **Deals service pattern:** `IDealService` / `DealService` — list endpoint supports both search (title contains) and stage filter. Stage filter uses `Enum.TryParse` with `ignoreCase: true`; unknown values are silently skipped (no 400).
- **Pipeline summary query:** `GetDealSummaryAsync` groups `_db.Deals` by `Stage` in a single EF `GroupBy` → `Select`, then excludes `ClosedLost` from `TotalPipelineValue` client-side after the DB round-trip.
- **ContactName in DealDto:** Used `d.Contact.FirstName + " " + d.Contact.LastName` in Select projections — avoids translating the unmapped `FullName` computed property through a navigation.
- **Routing:** `GET /api/deals/summary` is declared before `GET /api/deals/{id:guid}` to avoid routing conflicts with the GUID constraint.
- **OwnerId:** Set from the JWT `sub` / `NameIdentifier` claim at the controller layer, passed down to `CreateDealAsync(request, userId)`.

### 2026-03-25: EF Core Migrations — InitialCreate

- **Design-time factory pattern:** `DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>` lives in `WarpBusiness.Api/Data/`. It reads `ConnectionStrings__warpbusiness` env var, falling back to `Host=localhost;Port=5432;Database=warpbusiness_dev;Username=postgres;Password=postgres`. EF tooling auto-discovers it; no Aspire required.
- **Connection string key:** Program.cs uses `"warpbusiness"` (not `"DefaultConnection"`). The factory env var must match: `ConnectionStrings__warpbusiness`.
- **Migration command:** `dotnet ef migrations add <Name> --project WarpBusiness.Api\WarpBusiness.Api.csproj --startup-project WarpBusiness.Api\WarpBusiness.Api.csproj --output-dir Data\Migrations`
- **Auto-apply pattern:** Added `await db.Database.MigrateAsync()` inside `IsDevelopment()` guard immediately after `builder.Build()`, before `MapDefaultEndpoints()`. Scoped via `app.Services.CreateScope()`.
- **Migration scope:** `InitialCreate` captures all 11 tables: AspNetRoles, AspNetUsers, AspNetRoleClaims, AspNetUserClaims, AspNetUserLogins, AspNetUserRoles, AspNetUserTokens + Companies, Contacts, Deals, Activities.
- **Design package:** `Microsoft.EntityFrameworkCore.Design` was already present in the csproj with `<PrivateAssets>all</PrivateAssets>` (correct — keeps it dev-only).

### 2026-03-25: CustomerPortal Project Created

- **Project type:** Blazor Web App (.NET 10, InteractiveServer mode) for customer self-service
- **Project structure:** WarpBusiness.CustomerPortal is a separate, externally-accessible container with its own Program.cs, Services, and Components
- **AppHost registration:** Used `.WithExternalHttpEndpoints()` to mark CustomerPortal as the only externally-accessible endpoint; WarpBusiness.Web and WarpBusiness.Api remain internal
- **Services:**
  - `CustomerApiClient`: HttpClient wrapper configured with Aspire service discovery (`https+http://api`). Methods include LoginAsync, GetMyContactAsync, RefreshTokenAsync
  - `CustomerAuthState`: Singleton state service with reactive `OnChange` event for Blazor component re-rendering on auth changes
- **Pages:** Login (with email/password form), Home (dashboard), MyProfile (displays ContactDto fields), MyDeals (placeholder)
- **NavMenu:** Custom nav with auth state subscriber — renders user email and sign-out button when authenticated, redirects unauthenticated users to /login
- **Auth pattern:** CustomerApiClient stores access token and sets Authorization header; CustomerAuthState manages app-level auth state
- **API communication:** All API calls use the same JWT auth pattern as WarpBusiness.Web — customers authenticate with their email/password (Local provider) or OIDC providers (Keycloak/Microsoft)

### 2026-03-25: Activity Service and Controller Implemented

- **Entity shape:** `Activity` uses `Title` (not Subject), `Notes` (not Description), `ScheduledAt` (not DueDate). `IsCompleted` is a computed property from `CompletedAt.HasValue` — not stored. No direct `CompanyId` on Activity.
- **DTO mapping:** DTOs use spec-friendly names (`Subject`, `Description`, `DueDate`) that map to entity fields. `CompanyId`/`CompanyName` are derived from `Contact.Company` or `Deal.Company` via navigations.
- **Company filtering:** Activities have no direct `CompanyId`. Filter by `a.Contact.CompanyId == companyId || a.Deal.CompanyId == companyId` using included navigations.
- **Projection pattern:** Used `.Include().AsNoTracking().ToListAsync()` then in-memory `MapToDto()` to avoid EF translation issues with null-conditional chains on navigations.
- **CompleteActivityAsync:** Sets `CompletedAt = DateTimeOffset.UtcNow`; idempotent on already-completed activities.
- **UpdateActivityAsync:** Clears `CompletedAt` when `IsCompleted = false`, sets it when toggled to true and not yet set.
- **CreateActivityAsync:** Sets both `OwnerId` and `CreatedBy` from the JWT sub/NameIdentifier claim (passed from controller).

### 2026-03-25: Customer Self-Service and Admin APIs

- **GET /api/contacts/me:** Added to ContactsController for customers to fetch their own contact record via JWT email claim. Route placed before `{id:guid}` to prevent routing conflicts. `IContactService.GetContactByEmailAsync` queries by lowercase email with `.Include(c => c.Company)` and maps to ContactDto using existing Select projection pattern.
- **AdminController:** New controller under `/api/admin` with `[Authorize(Roles = "Admin")]`. Three endpoints:
  - `GET /api/admin/users`: Returns all users with roles, AuthProvider, and LastLoginAt metadata
  - `POST /api/admin/users/{userId}/roles`: Adds or removes a role (creates role if missing via RoleManager)
  - `DELETE /api/admin/users/{userId}`: Deletes user with last-admin guard (Conflict if last admin)
- **ApplicationUser.AuthProvider:** Added nullable string property to track auth source ("Local", "Keycloak", "Microsoft"). Defaults to "Local" for backward compatibility.
### 2026-03-26: Customer Self-Service and Admin APIs

- **GET /api/contacts/me:** Added to ContactsController for customers to fetch their own contact record via JWT email claim. Route placed before `{id:guid}` to prevent routing conflicts. `IContactService.GetContactByEmailAsync` queries by lowercase email with `.Include(c => c.Company)` and maps to ContactDto using existing Select projection pattern.
- **AdminController:** New controller under `/api/admin` with `[Authorize(Roles = "Admin")]`. Three endpoints:
  - `GET /api/admin/users`: Returns all users with roles, AuthProvider, and LastLoginAt metadata
  - `POST /api/admin/users/{userId}/roles`: Adds or removes a role (creates role if missing via RoleManager)
  - `DELETE /api/admin/users/{userId}`: Deletes user with last-admin guard (Conflict if last admin)
- **ApplicationUser.AuthProvider:** Added nullable string property to track auth source ("Local", "Keycloak", "Microsoft"). Defaults to "Local" for backward compatibility.
- **SetRoleRequest DTO:** Added to WarpBusiness.Shared.Auth.AuthDtos.cs as `record SetRoleRequest(string Role, bool Add)` for role toggle API contract.

### 2026-03-26: Activity Service and Controller Implemented

- **Entity shape:** `Activity` uses `Title` (not Subject), `Notes` (not Description), `ScheduledAt` (not DueDate). `IsCompleted` is a computed property from `CompletedAt.HasValue` — not stored. No direct `CompanyId` on Activity.
- **DTO mapping:** DTOs use spec-friendly names (`Subject`, `Description`, `DueDate`) that map to entity fields. `CompanyId`/`CompanyName` are derived from `Contact.Company` or `Deal.Company` via navigations.
- **Company filtering:** Activities have no direct `CompanyId`. Filter by `a.Contact.CompanyId == companyId || a.Deal.CompanyId == companyId` using included navigations.
- **Projection pattern:** Used `.Include().AsNoTracking().ToListAsync()` then in-memory `MapToDto()` to avoid EF translation issues with null-conditional chains on navigations.
- **CompleteActivityAsync:** Sets `CompletedAt = DateTimeOffset.UtcNow`; idempotent on already-completed activities.
- **UpdateActivityAsync:** Clears `CompletedAt` when `IsCompleted = false`, sets it when toggled to true and not yet set.
- **CreateActivityAsync:** Sets both `OwnerId` and `CreatedBy` from the JWT sub/NameIdentifier claim (passed from controller).



### 2026-03-26: Custom Fields for Contacts

- **Domain:** `CustomFieldDefinition` (admin-defined: Name, EntityType, FieldType, SelectOptions as JSON, IsRequired, DisplayOrder, IsActive) and `CustomFieldValue` (ContactId + FieldDefinitionId + Value string). Contact gets `CustomFieldValues` navigation.
- **EF Config:** Unique index on `(EntityType, Name)` for definitions; unique index on `(ContactId, FieldDefinitionId)` for values; cascade delete on both FK sides.
- **Storage:** `SelectOptions` is stored as a JSON string array in a varchar(2000) column; serialized/deserialized via `System.Text.Json`.
- **`GetValuesForContactAsync`:** Returns ALL active definitions for "Contact" with the contact's value (null if not set). Gives the UI a full form shape every time.
- **Batch fetch in `GetContactsAsync`:** After paging contacts, fetch all values for the page's contact IDs in one query; join in-memory. Avoids N+1.
- **Upsert pattern:** `UpsertValuesAsync` loads existing values for (contactId, defIds), updates matching rows, inserts new ones, single `SaveChangesAsync`.
- **ContactService circular dependency avoided:** `ContactService` depends on `ICustomFieldService`; both are Scoped — no circular issue.
- **409 on delete:** `CustomFieldsController.DeleteDefinition` checks `AnyAsync` for values before delegating to service. Service itself does a straight remove (no check) to keep it clean.
- **ContactDto breaking change:** Added `IReadOnlyList<CustomFieldValueDto> CustomFields` as last positional record parameter — all existing constructors in `ContactService` updated.
- **Migration command:** `dotnet ef migrations add AddCustomFields --project . --startup-project .` from `src/WarpBusiness.Api/`.


### 2026-03-26: CRM Backend Extracted to WarpBusiness.Plugin.Crm

- **Extraction scope:** All 6 domain entities, 6 EF configs, 5 service interfaces + implementations, and 5 controllers moved from WarpBusiness.Api to WarpBusiness.Plugin.Crm.
- **CrmDbContext:** New DbContext with HasDefaultSchema("crm") and ApplyConfigurationsFromAssembly. All 6 CRM DbSets added. Services now inject CrmDbContext instead of ApplicationDbContext.
- **ApplicationDbContext:** Stripped to Identity tables + RefreshToken only. No more CRM-domain using statements or DbSets.
- **CrmModule.ConfigureServices:** Registers CrmDbContext (using warpbusiness connection string, falling back to DefaultConnection) plus all 5 CRM services as Scoped.
- **CrmModule.Configure:** Calls db.Database.Migrate() to auto-apply the crm schema migrations at startup.
- **ServiceCollectionExtensions:** AddCustomModules now accepts IEnumerable<ICustomModule>? firstPartyModules — calls ConfigureServices on each and registers them in ModuleRegistry with "built-in" source. Employee and CRM modules both pass through this path.
- **Program.cs:** Removed explicit CRM service registrations; added AddApplicationPart(typeof(CrmModule).Assembly); both first-party modules registered via AddCustomModules(firstPartyModules: ...). The explicit mployeeModule.ConfigureServices(...) and mployeeModule.Configure(app) calls removed (now handled by AddCustomModules/UseCustomModules).
- **EF migrations:** ExtractCrmToPlugin (Api) is an intentional no-op — no drop tables. InitialCrm (Plugin.Crm) creates all 6 CRM tables in the crm schema fresh.
- **Connection string:** CrmModule tries "warpbusiness" first to match existing Aspire-registered connection; falls back to "DefaultConnection" for third-party deployments.
- **Nav items:** CrmModule.GetNavItems() returns Contacts/Companies/Deals — now served via ModuleRegistry through GET /api/modules/nav-items. Blazor frontend no longer needs hardcoded CRM nav links.


### 2026-03-26: Kubernetes Manifests, Makefile, and Skaffold Config

- **k8s/ directory (kustomize-based):** Namespace warp-business, PostgreSQL 16 StatefulSet + headless Service + 5Gi PVC, Keycloak 24 Deployment + Service (start-dev, optional), API/Web/Portal Deployments with ConfigMaps, nginx Ingress for api/app/portal.warp-business.local.
- **Secrets handling:** secrets.yaml.template with base64 placeholders; k8s/secrets.yaml gitignored. API deployment uses $(WARP_DB_PASSWORD) K8s env var substitution (password injected from Secret, username hardcoded as warpuser).
- **Aspire service discovery in K8s:** services__api__http__0=http://warp-api:8080 set in Web and Portal ConfigMaps — no code changes needed.
- **imagePullPolicy: Never** on all app containers — images built locally, not pulled from registry.
- **Makefile targets:** build, load-kind, deploy (guards for secrets.yaml), undeploy, status, logs-api/web/portal, restart, clean.
- **skaffold.yaml:** Local dev loop with auto-rebuild + port-forward (5001/5002/5003).
