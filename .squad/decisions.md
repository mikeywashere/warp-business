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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here  
- Keep history focused on work, decisions focused on direction
