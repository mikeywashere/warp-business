# Squad Decisions

## Active Decisions

### 2026-03-25: Project Bootstrap

- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core
- **ORM:** Entity Framework Core approved
- **Scope:** Start with CRM module; expand to full business management system
- **Aspire Orchestration:** Use .NET Aspire for service orchestration and dev-inner-loop. Architecture: WarpBusiness.AppHost (entry point), WarpBusiness.ServiceDefaults (shared telemetry/health), WarpBusiness.Api (backend), WarpBusiness.Web (Blazor), WarpBusiness.CustomerPortal (external), WarpBusiness.Shared (DTOs), WarpBusiness.Tests (xUnit)
- **Owner:** Michael R. Schmidt

### 2026-03-25: Multi-Provider OIDC Authentication

**Status:** Implemented

- **Architecture:** Config-driven provider system with three modes: Local (ASP.NET Core Identity + JWT), Keycloak (OIDC), Microsoft (OIDC)
- **Selection:** Admin chooses via `appsettings.json` → `AuthProvider:ActiveProvider`
- **Implementation:** `AuthProviderExtensions.AddWarpAuthentication()` replaces hardcoded JWT setup. `ExternalIdentityMapper` auto-provisions shadow `ApplicationUser` on OIDC first login.
- **User Provisioning:** Local users register via `/api/auth/register`. OIDC users auto-provision on external login. All users land in same `AspNetUsers` table.
- **Frontend Discovery:** `GET /api/auth/provider` returns active provider info so UI can adapt (login form for Local, redirect for OIDC)
- **Trade-off:** Only one provider active at a time by design (simpler ops)
- **Owner:** Bishop (Auth Specialist)

### 2026-03-25: Refresh Token Implementation (Bearer + HttpOnly Cookie)

**Status:** Implemented

- **Pattern:** Bearer tokens in Authorization header (short-lived, ~15 min). Refresh tokens in HttpOnly cookies (`warp_refresh`) with automatic rotation on use.
- **Client Behavior:** On 401, call `POST /api/auth/refresh` (no body — cookie auto-included). Response contains new bearer token. Retry original request.
- **Logout:** `POST /api/auth/logout` revokes server-side refresh tokens (clears cookie).
- **Implementation:** WarpApiClient and CustomerApiClient both implement `TryRefreshAsync()` and `SendWithRefreshAsync()` wrapper for automatic 401 handling.
- **ClientPrincipal Flow:** All components route through authenticated API calls; 401 automatically triggers refresh. No component-level auth logic needed.
- **Owner:** Bishop (Auth Specialist), Vasquez (Frontend Implementation)

### 2026-03-25: CRM Domain Model

**Status:** Implemented

- **Entities:** Contact, Company, Deal, Activity with relationships:
  - Contact → Company (many-to-one, nullable)
  - Contact ↔ Deal (many-to-many via navigation)
  - Contact ↔ Activity (one-to-many)
  - Company ↔ Deal (one-to-many)
  - Deal ↔ Activity (one-to-many)
  - Activity uses `Title`/`Notes`/`ScheduledAt`, no direct `CompanyId` (derived from Contact/Deal)
- **DTOs:** Record-based contracts with PagedResult<T> pagination pattern
- **Service Pattern:** Async with CancellationToken. Query operations use `AsNoTracking()`. EF projections avoid null-conditional translation errors on navigations.
- **Ownership:** `OwnerId` (string) links to ApplicationUser.Id; `CreatedBy` field populated from JWT sub claim
- **Owner:** Hicks (Backend Dev)

### 2026-03-25: Integration Test Strategy

**Status:** Approved

- **Framework:** xUnit with `WebApplicationFactory<Program>` + in-memory EF Core database
- **Infrastructure:** `WarpTestFactory` replaces PostgreSQL with in-memory DB, injects test JWT settings. `AuthHelper` provides register-and-token and SetBearerToken extension.
- **Isolation:** Each test class gets its own named in-memory database. Full ASP.NET Core pipeline runs (auth, authorization, validation, routing).
- **Constraints:** In-memory EF does not enforce relational constraints (FK, unique). Tests requiring constraint violation must use Testcontainers (future).
- **Gotcha:** Test classes share one factory instance. Keep test data unique (Guid.NewGuid() in identifiers) to avoid state pollution.
- **Owner:** Hudson (Tester)

### 2026-03-25: Blazor Component Patterns

**Status:** Implemented

- **EditForm + InputControls:** Use EditForm for inline edit modes, InputText/InputSelect/InputDate for fields
- **Pagination:** Numbered pages + prev/next buttons. Client-side filtering on current page (debounce 350ms for search)
- **Modal Confirmation:** Confim-before-delete using Bootstrap modal with callback handler
- **API Client:** Typed HttpClient registered via Aspire service discovery (`https+http://api`). All calls flow through `SendWithRefreshAsync()` for transparent token refresh.
- **NavLink State:** Use NavLink for active route highlighting; NavMenu subscribes to AuthStateService.OnChange for auth state updates
- **Table Rendering:** Paged tables with linked titles, badges for status/role, conditional columns (null → "—")
- **Alert Banners:** Inline success/error feedback using Bootstrap alerts with dismissal
- **Routing Gotchas:** 
  - `@page` variable name triggers Razor directive parsing (use `pageNum`/`pageText` not `page`)
  - Escaped quotes in string interpolation within `@onclick` attributes fail — use plain anchors instead
  - Can't use escaped quotes in `@onclick` lambdas — extract literals to const
- **Owner:** Vasquez (Frontend Dev)

### 2026-03-25: Deal Management Features

**Status:** Implemented

- **DealList:** Paged table with title filter (debounced), pipeline summary bar top showing TotalPipelineValue and per-stage count/value
- **Pipeline Summary:** Backend groups by Stage; frontend excludes ClosedLost from total
- **DealDetail:** Dual-mode (view/edit) with create branch (`Id == Guid.Empty` detects new). Edit mode is inline toggle.
- **Fields:** Title (required), Stage (dropdown), Value ($), Probability (%), Expected Close Date (picker)
- **Delete:** Confirmation modal before destructive action
- **API Routes:** All methods use `SendWithRefreshAsync()` pattern. Summary endpoint is `GET /api/deals/summary` (declared before `{id:guid}` to avoid routing conflict)
- **Owner:** Vasquez (Frontend Dev)

### 2026-03-25: Activity Service Design

**Status:** Implemented

- **Entity:** Title, Notes, ScheduledAt, CompletedAt. `IsCompleted` is computed from `CompletedAt.HasValue` (not stored).
- **Company Filtering:** No direct CompanyId on Activity. Filter via `Contact.CompanyId == companyId || Deal.CompanyId == companyId` using included navigations.
- **Completion Logic:** `UpdateActivityAsync` handles IsCompleted toggle; clears CompletedAt when toggled false, sets it when toggled true
- **DTO Mapping:** Spec-friendly names (Subject → Title, Description → Notes, DueDate → ScheduledAt). CompanyId/CompanyName derived from navigations.
- **Service Pattern:** Async with CancellationToken. EF projections use Include().AsNoTracking() then in-memory Select to avoid translation issues on null-conditional navigations.
- **Owner:** Hicks (Backend Dev)

### 2026-03-26: Customer Portal Feature Parity

**Status:** Implemented

- **Portal 401→Refresh→Retry:** CustomerApiClient implements identical refresh flow to WarpBusiness.Web: TryRefreshAsync() + SendWithRefreshAsync() wrapper
- **MyProfile Edit:** Inline toggle with EditForm for FirstName, LastName, Phone, JobTitle. Email shown read-only (auth-linked).
- **Updates:** UpdateMyContactAsync() passes existing Contact ID; preserves Status/CompanyId/Email unchanged
- **Logout:** Calls LogoutAsync() for server-side token revocation (mirrors Web app)
- **Owner:** Vasquez (Frontend Dev)

### 2026-03-26: Custom Fields for Contacts

**Status:** Implemented

- **Domain:** `CustomFieldDefinition` (admin-defined: Name, EntityType, FieldType, SelectOptions as JSON, IsRequired, DisplayOrder, IsActive) and `CustomFieldValue` (ContactId + FieldDefinitionId + Value string). Contact gets `CustomFieldValues` navigation.
- **Storage:** SelectOptions stored as JSON string array in varchar(2000) column; serialized/deserialized via System.Text.Json. Values always stored as strings.
- **Service Pattern:** `ICustomFieldService` provides definition CRUD, value retrieval returning all active definitions with contact's values (nulls for unset), and batch upsert. Batch fetch in `GetContactsAsync` loads all values for paged contacts in one query, joins in-memory (no N+1).
- **API:** GET /api/custom-fields?entityType=Contact (all roles), POST/PUT/DELETE /api/custom-fields/{id} (Admin only). Delete returns 409 Conflict if values exist.
- **Database:** Unique index on (EntityType, Name) for definitions; (ContactId, FieldDefinitionId) for values. Cascade delete configured. Migration: AddCustomFields.
- **EF Config:** Both entities properly configured with constraints and indexes. ContactDto extended with `IReadOnlyList<CustomFieldValueDto> CustomFields` parameter.
- **Trade-offs:** Values stored as strings always (simple, no per-type schema migration); single active provider per EntityType (sufficient for MVP).
- **Owner:** Hicks (Backend Dev)

### 2026-03-26: Custom Fields UI Design

**Status:** Implemented

- **Component Architecture:** `CustomFieldInput.razor` created as reusable shared component in WarpBusiness.Web/Components/Shared/. Renders type-aware inputs (Text, Number, Date, Boolean checkbox, Select dropdown). Required field indicator (*) rendered dynamically.
- **Global Registration:** Added `@using WarpBusiness.Web.Components.Shared` to _Imports.razor for global access across app.
- **Delete Conflict Handling:** `WarpApiClient.DeleteCustomFieldDefinitionRawAsync` returns int status code, allowing admin page to distinguish 409 (field has data) from other errors and display "deactivate instead" message.
- **Admin Management Form:** Inline form below table (not modal) using `_showForm` flag + `_editingId` nullable to distinguish create vs. edit. Single form reused for both modes.
- **Contact Detail Integration:** Loads field definitions in parallel with contact via Task.WhenAll (no extra latency). View mode shows custom fields section; edit mode displays `<CustomFieldInput>` per definition, ordered by DisplayOrder. Save builds `UpsertCustomFieldValueRequest` list from dict.
- **Portal Replication:** CustomerPortal/MyProfile.razor replicates custom field input logic inline (avoids cross-project dependency). Loads contact + defs in parallel in OnInitializedAsync.
- **Razor Quote Gotcha:** Boolean checkbox `@onchange` handlers cannot inline "true"/"false" string literals inside attribute — Razor terminates at inner quote. Always extract to named method. Confirmed again in this sprint.
- **API Client:** Extended WarpApiClient with GetCustomFieldDefinitionsAsync, Create/Update/Delete methods. All use SendWithRefreshAsync() pattern.
- **Navigation:** Custom Fields link added under Admin section in NavMenu (after Users link).
- **Trade-offs:** Entity type hardcoded to "Contact" in admin UI (multi-entity support is future); portal replicates input logic instead of sharing (acceptable for contained scope).
- **Owner:** Vasquez (Frontend Dev)

### 2026-03-26: Admin Role Required for All CRM Delete Operations

**Status:** Implemented

- **Decision:** All CRM DELETE endpoints require the `Admin` role. Authenticated non-Admin users receive `403 Forbidden`.
- **Affected endpoints:** DELETE /api/contacts/{id}, DELETE /api/companies/{id}, DELETE /api/deals/{id}, DELETE /api/activities/{id}
- **Rationale:** Delete operations are destructive and irreversible. Any authenticated user being able to delete CRM records poses significant data integrity risk. Restricting to Admin follows principle of least privilege.
- **Implementation:** Added `[Authorize(Roles = "Admin")]` on each DELETE action method. Class-level `[Authorize]` ensures authentication; method-level attribute additionally requires Admin role (AND semantics in ASP.NET Core).
- **Alternatives Considered:** Manager role was rejected (deletes too permanent); soft delete out of scope for this fix.
- **Impact on Tests:** Updated integration tests to assert Admin DELETE → 204 No Content, non-Admin DELETE → 403 Forbidden. Fixed pre-existing UpdateContact test failures caused by IDOR protection fix.
- **Owner:** Hicks (Backend Dev)
- **PR:** fix/crm-delete-authorization (#7)

### 2026-03-26: Playwright E2E Test Suite

**Status:** Implemented

- **Project:** WarpBusiness.Tests.E2E (net10.0, 18 files, NUnit + Microsoft.Playwright.NUnit 1.51.0)
- **Framework Choice:** NUnit chosen over xUnit for SetUpFixture lifecycle mapping to browser → context → page hierarchy.
- **Browser Lifecycle:** Shared IBrowser per test class (startup cost), fresh IBrowserContext per test (auth state isolation).
- **Configuration:** E2E_BASE_URL env var (default http://localhost:5002) allows CI/multi-dev compatibility.
- **Auto-Bootstrapping:** AuthHelper.LoginAsync() auto-registers test user on login failure (no manual data seeding needed).
- **Test Structure:** PlaywrightSetup (one-time install), PageTestBase (lifecycle), AuthHelper (login/register), page objects (LoginPage, ContactsPage, CompaniesPage, DealsPage, EmployeesPage), 6 test classes.
- **Selectors:** GetByLabel/GetByRole/GetByPlaceholder/CSS (no data-testid attributes). Labels and roles exist; CSS class `table` used consistently.
- **Filtering:** [Category("E2E")] allows `dotnet test --filter "Category!=E2E"` to exclude from normal runs.
- **App Health:** RequireAppAsync() skips tests with Assert.Ignore if app not reachable (prevents false red builds in CI).
- **Critical Fix:** Added .gitignore negation `!src/WarpBusiness.Tests.E2E/` (VS *.e2e pattern was blocking directory).
- **Owner:** Hudson (Tester)
- **PR:** https://github.com/mikeywashere/warp-business/pull/8 (merged)

### 2026-03-26T20:10:00Z: Code Review Critical Findings
**By:** Ripley (code review)
**What:** Full code review completed. Critical issues identified:
1. Database credentials committed to appsettings.json (Password=OhHowSad6) — ROTATE IMMEDIATELY
2. IDOR vulnerability in ContactsController PUT /api/contacts/{id} — portal users can modify any contact
3. Email case-sensitivity bug breaks /api/contacts/me for mixed-case emails
4. ExternalIdentityMapper does not set AuthProvider on OIDC-provisioned users
5. K8s deployments missing resource limits and liveness probes
6. Race condition window in refresh token rotation (no optimistic concurrency)
7. Missing input validation attributes on request DTOs
**Why:** Code review findings — must track for remediation

### 2026-03-26: Security Fix Decisions — Code Review Remediation

**By:** Hicks (Backend Dev)  
**Status:** Implemented (PR #3)

#### 1. appsettings.json — Credential Placeholder

**Decision:** Add a `DefaultConnection` key to `appsettings.json` with `Password=CHANGE_ME` instead of leaving the key absent.

**Rationale:** Making the connection string schema explicit in source helps operators know what env vars to supply. The `CrmModule` already falls back to `DefaultConnection`; this documents that path without committing real credentials.

**Impact:** No runtime behavior change. Operators must still override via env var or Aspire secrets.

#### 2. IDOR Fix — ContactsController PUT Ownership Check

**Decision:** Ownership enforcement lives in the controller, not the service layer.

**Rationale:** The service layer (`ContactService.UpdateContactAsync`) is called by multiple callers — admin tools, background jobs, etc. — that legitimately bypass ownership checks. Authorization is a controller/HTTP concern. The pattern: load the resource via service → compare JWT email claim → `Forbid()` on mismatch for non-Admin/non-Manager roles.

**Impact:** Portal users (`Customer` role or authenticated without Admin/Manager) now receive `403 Forbidden` when attempting to modify another contact's record.

#### 3. Email Normalization — Service Layer, Not Controller

**Decision:** `ToLowerInvariant()` applied in `ContactService.CreateContactAsync` and `UpdateContactAsync`, and in `EmployeeService.CreateAsync` and `UpdateAsync`.

**Rationale:** Normalization belongs with data persistence, not with HTTP handling. Any caller (tests, admin tools, background sync) must get the same normalization guarantee. `GetContactByEmailAsync` already queries by lowercase; this closes the round-trip consistency gap.

**Trade-off:** Existing contacts with mixed-case emails will not be retroactively normalized. A one-time migration script may be needed if mixed-case emails exist in production.

#### 4. Data Annotations — Positional Record Parameters

**Decision:** Validation attributes applied directly to positional record constructor parameters in `CreateContactRequest`, `UpdateContactRequest`, `RegisterRequest`, and `LoginRequest`.

**Rationale:** ASP.NET Core model binding applies `[ApiController]` automatic model validation before the action runs, so no changes to `Program.cs` or controllers are needed. Attributes on positional params are fully supported since C# 9.

**Scope:** `MaxLength` values match EF Core column constraints (100 for names, 256 for email, 50 for phone, 200 for job title). `Password` min/max follows OWASP guidance (8–128).

### 2026-03-26: Auth Provider Assignment and K8s Infrastructure Fixes

**By:** Bishop (Auth Specialist)  
**Status:** Implemented (PR #4)

#### 1. AuthProvider Assignment on OIDC Provisioning

**Decision:** Set `AuthProvider = provider.ToString()` on new `ApplicationUser` records provisioned from OIDC providers. Also update `AuthProvider` on returning users whose value is `"Local"` or empty (accounts created via local registration that later authenticate via OIDC).

**Rationale:** `AuthProvider` defaults to `"Local"` on `ApplicationUser`. Without explicit assignment, all OIDC-provisioned users silently misidentify as local users, breaking admin observability (UserManagement page shows wrong provider) and any future provider-specific logic.

**Value used:** `provider.ToString()` — this produces `"Keycloak"` or `"Microsoft"` matching the `AuthProviderType` enum, consistent with how `AuthController.GetProvider` returns the active provider name and how `AdminController` displays it.

#### 2. K8s Resource Limits

**Decision:** Add resource requests/limits to all three deployments using tiered sizing:
- **API** (256Mi/200m → 768Mi/750m): handles EF Core, business logic, auth pipeline
- **Web** (256Mi/100m → 512Mi/500m): Blazor Server keeps per-circuit state in memory
- **Portal** (128Mi/100m → 256Mi/500m): lightweight, minimal service

**Rationale:** Without resource limits, noisy-neighbor scenarios can starve other pods. Without requests, the scheduler cannot make good placement decisions. These are production-safety requirements, not nice-to-haves.

#### 3. Liveness Probes

**Decision:** Add `livenessProbe` to web and portal deployments matching the API's existing pattern. Path `/`, port 8080, initialDelaySeconds 30, periodSeconds 30, failureThreshold 3.

**Rationale:** Without liveness probes, Kubernetes cannot detect deadlocked or hung Blazor Server processes and will not restart them automatically. The 30s initial delay gives the app time to warm up before probing begins.

#### 4. Secrets Template Sanitization

**Decision:** Replace all base64-encoded values in `k8s/secrets.yaml.template` with `REPLACE_WITH_BASE64_ENCODED_VALUE` and standardize the header comment block.

**Rationale:** The previous template contained base64-encoded strings that decoded to recognizable values (`warpuser`, `CHANGE_ME_strong_password`, a dev JWT key). These could be mistaken for valid values or accidentally deployed. The new placeholders make it unambiguously clear that real values must be substituted. Added `keycloak-admin-password` key that was missing.

#### Trade-offs Considered

- **`provider.ToString()` vs hardcoded scheme names:** Using the enum `.ToString()` keeps it in sync with the config-driven provider system. If a new provider is added to `AuthProviderType`, it automatically gets the right label.
- **Resource values:** Conservative but not too tight. API gets more headroom because it runs EF migrations on startup and handles heavier workloads.

### 2026-03-26: Test Coverage Audit — CRM Authorization Gaps

**By:** Hudson (Tester)  
**Status:** Identified (PR #5)

#### Systemic Issue: Controllers Without Role-Based Authorization

**Finding:** CRM delete endpoints (Companies, Deals, Activities, Contacts) lack `[Authorize(Roles = "Admin")]` annotation. Only class-level `[Authorize]` present — any authenticated user can delete any resource.

**Contrast:** `EmployeesController` correctly uses `[Authorize(Roles = "Admin")]` on destructive actions.

**Risk:** Test requests like "add admin-only delete tests" create false confidence if blindly accepted. Tests should reflect the actual authorization model, not the desired one.

#### Affected Endpoints
- `DELETE /api/companies/{id}` — no role restriction
- `DELETE /api/deals/{id}` — no role restriction
- `DELETE /api/activities/{id}` — no role restriction
- `DELETE /api/contacts/{id}` — no role restriction (IDOR fix was about ownership, not roles)

#### Recommendation

Ripley or Hicks should audit all `[Authorize]` annotations on destructive endpoints (DELETE, deactivate, bulk operations) and apply role restrictions where business logic demands them.

#### What Was Done

Tests written documenting actual behavior, not assumed behavior. Test names updated to `_WhenAuthenticated` / `_WhenNotAuthenticated` instead of `_WhenAdmin` / `_WhenNotAdmin` for CRM delete endpoints.

### 2026-03-26: CRM Backend Extraction to WarpBusiness.Plugin.Crm

**By:** Hicks (Backend Dev)  
**Status:** Implemented

**Summary:** All CRM backend code (entities, configs, services, controllers) moved from `WarpBusiness.Api` into `WarpBusiness.Plugin.Crm`. New `CrmDbContext` with `crm` schema; migration `InitialCrm` for fresh databases. Connection string tries `"warpbusiness"` first (Aspire key), falls back to `"DefaultConnection"`.

**Key Decision:** Empty `ExtractCrmToPlugin` migration in Api (no drop tables) — existing production data stays intact. Fresh databases served by `CrmDbContext` in `crm` schema.

**First-party module pattern:** `AddCustomModules` accepts `IEnumerable<ICustomModule>? firstPartyModules`; CRM and Employee modules registered this way, appearing in `ModuleRegistry` and contributing nav items via `GET /api/modules/nav-items`.

**Blazor pages:** Contacts/Companies/Deals pages stay in `WarpBusiness.Web` (use `WarpApiClient` with auth/refresh logic tied to Web project).

**CustomFieldsController:** Injects `CrmDbContext` directly for duplicate-name/delete-guard checks before delegating to service (authz is controller concern).

### 2026-03-26: Custom Fields Integration Test Coverage

**By:** Hudson (Tester)  
**Status:** Implemented

#### 1. Duplicate-name guard in controller

`CustomFieldService` has no uniqueness check. Controller performs `AnyAsync` pre-flight before calling service, returning `409 Conflict` when a field with same `Name + EntityType` already exists.

#### 2. Shared in-memory DB — Guid-suffix field names

All tests in `CustomFieldsControllerTests` share one `WarpTestFactory` instance and same in-memory database. Field definition names suffixed with `Guid.NewGuid():N` to prevent cross-test collisions.

#### 3. GET contact returns all active definitions

`GetValuesForContactAsync` always returns every active `Contact` field definition, with `Value = null` for unset fields.

### 2026-03-26: Test Factory Pattern for CRM Plugin

**By:** Hudson (Tester)  
**Status:** Implemented

**Pattern:** `WarpTestFactory` calls `AddApplicationPart` + plugin `ConfigureServices` for each first-party plugin. Tests reference `WarpBusiness.Plugin.Crm` directly.

**Details:**
- Dummy `ConnectionStrings:warpbusiness` setting via `builder.UseSetting(...)` before `ConfigureServices`
- `ReplaceWithInMemory<TContext>` helper swaps `CrmDbContext`, `EmployeeDbContext`, and `ApplicationDbContext` for in-memory equivalents
- `<ProjectReference>` for each plugin in Tests.csproj so DbContext types accessible at compile time
- `AddApplicationPart` for plugin assemblies already in Program.cs, inherited by `WebApplicationFactory`

### 2026-03-28: Catalog Product Search Endpoint & Cross-Plugin DTO

**By:** Hicks (Backend Dev)  
**Status:** Implemented (Branch: `feature/catalog-search-and-invoice-prep`)

- **Endpoint:** `GET /api/catalog/products/search?q={term}&limit={count}` — lightweight search for autocomplete/typeahead
- **Filters:** Active products only. Searches Name, Sku, Brand (case-insensitive)
- **Response:** `CatalogItemSearchResult` — minimal fields (Id, Name, Sku, BasePrice, Currency, ProductType, PrimaryImageUrl) plus inline active variant summaries
- **Limits:** Default 10, max 50, clamped server-side
- **Auth:** Standard `[Authorize(Policy = "RequireActiveTenant")]`
- **CatalogItemReference DTO:** Lives in `WarpBusiness.Shared/Catalog/`. Record type: ProductId, ProductName, ProductSku, VariantId?, VariantSku?, UnitPrice, Currency. Enables Invoice plugin to reference catalog items without assembly coupling.
- **Rationale:** No schema changes, no migrations, no breaking changes to existing endpoints. Establishes cross-plugin reference pattern (GUID + denormalized names).

### 2026-03-28: Invoice Plugin Architecture

**By:** Ripley (Lead)  
**Status:** Proposed (Design: `.squad/decisions/inbox/ripley-invoice-plugin-design.md`)

- **Plugin:** `WarpBusiness.Plugin.Invoicing` with schema `invoicing`, Module ID `com.warpbusiness.invoicing`
- **4 Core Entities:** 
  - `Invoice` — root aggregate with customer ref, dates, financial summary, status lifecycle
  - `InvoiceLineItem` — three types (Manual, CatalogProduct, TimeEntry) via discriminator enum. All use snapshot pricing (no live catalog lookups)
  - `InvoicePayment` — tracks partial and full payments. Recording payment auto-transitions status
  - `InvoiceSettings` — per-tenant config for numbering (prefix + sequence with optimistic concurrency), defaults, company info
- **Lifecycle:** Draft → Sent → Paid/PartiallyPaid/Overdue/Cancelled/Void. Only Draft invoices are editable/deletable
- **Integration Pattern:** Loose coupling with Catalog and TimeTracking (GUID + denormalized names only, no cross-schema FKs). Zero runtime dependency on other plugins. Frontend orchestrates product/employee selection; Invoice backend stores only snapshots at line-item creation time
- **Financial Precision:** All monetary decimals use decimal(18,4)
- **Services & Controllers:** IInvoiceService, IInvoiceLineItemService, IInvoicePaymentService, IInvoiceSettingsService (async + CancellationToken pattern). Four controllers under `api/invoicing/` — Invoices (with /summary, /send, /cancel, /void), LineItems (nested), Payments (nested), Settings (Admin-only)
- **EF Config:** Tenant query filters, composite indexes on TenantId + key fields, enums stored as strings
- **Rationale:** Invoicing is a critical revenue workflow. Plugin isolation allows independent deployment and versioning. Loose coupling with CRM/Catalog enables future multi-tenancy isolation without schema explosion

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here  
- Keep history focused on work, decisions focused on direction
