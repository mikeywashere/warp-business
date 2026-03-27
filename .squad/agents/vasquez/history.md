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

### 2026-03-25: Contact Detail, Company List, Admin Pages

**Contact Detail Page (`ContactDetail.razor`):**
- Route: `/contacts/{Id:guid}` — Full detail view with inline edit mode toggle
- Read view: Shows all contact fields with proper formatting (email as link, status badges)
- Edit mode: EditForm with InputText/InputSelect controls, company picker dropdown from API
- Company picker loads via `GetCompaniesAsync()` on edit mode entry
- Delete confirmation modal before calling `DeleteContactAsync()`, redirects to `/contacts` on success
- 404 handling: "Contact not found" message with back link
- ContactList already linked names to detail view (line 54)

**Company List Page (`CompanyList.razor`):**
- Route: `/companies` — Paged table with search, inline create modal, delete actions
- Columns: Name (with website link if available), Industry, Contact Count badge
- Create modal: EditForm for all CompanyDto fields (Name, Website, Industry, Email, Phone, EmployeeCount)
- Delete: Shows warning if `ContactCount > 0`, only allows delete if zero
- Search/pagination pattern matches ContactList

**Admin User Management (`UserManagement.razor`):**
- Route: `/admin/users` with `@attribute [Authorize(Roles = "Admin")]`
- Top section shows active auth provider from `GET /api/auth/provider` with guidance note
- User table: FullName, Email, Provider badge, Roles (Admin = red badge), LastLoginAt
- Role actions: "Make Admin" / "Remove Admin" buttons per user
- Calls `SetUserRoleAsync(userId, "Admin", add/remove)`

**WarpApiClient Additions:**
- Contact methods: `GetContactAsync`, `UpdateContactAsync`, `DeleteContactAsync` (already existed, confirmed working)
- Company methods: `GetCompaniesAsync`, `CreateCompanyAsync`, `DeleteCompanyAsync`
- Admin methods: `GetUsersAsync`, `SetUserRoleAsync`

**Shared DTOs:**
- Added `UserSummaryDto` to `AuthDtos.cs` with Id, Email, FullName, Roles, Provider, LastLoginAt

**NavMenu Updates:**
- Companies link already present in CRM section
- New "ADMIN" section with "Users" link, only shown when `AuthState.Roles.Contains("Admin")`
- `IsAdmin` computed property checks roles from auth state

**Razor Gotcha:** Cannot use escaped quotes `\"` in @onclick lambda expressions — Razor parser treats backslash as escape character. Fixed by extracting "Admin" to const `AdminRole`.

### 2026-03-25: 401→Refresh→Retry Pattern + Server-Side Logout

**WarpApiClient Transparent Token Refresh:**
- Added `TryRefreshAsync()`: Calls `POST /api/auth/refresh` (cookie-based, no request body), updates bearer token in HttpClient.DefaultRequestHeaders on success, updates AuthStateService
- Added `SendWithRefreshAsync(Func<Task<HttpResponseMessage>>)`: Retry wrapper that catches 401, attempts refresh, retries original request once. On refresh failure, clears auth state and redirects to `/login`
- All authenticated methods now flow through `SendWithRefreshAsync()`: GetContactsAsync, GetContactAsync, CreateContactAsync, UpdateContactAsync, DeleteContactAsync, GetCompaniesAsync, CreateCompanyAsync, DeleteCompanyAsync, GetUsersAsync, SetUserRoleAsync
- Added `LogoutAsync()`: Calls `POST /api/auth/logout` to revoke server-side refresh tokens, clears bearer header + AuthState regardless of response (silent fail)
- Injected `NavigationManager` in constructor (DI resolves automatically alongside HttpClient via `AddHttpClient<WarpApiClient>` registration)

**NavMenu Logout Update:**
- Logout handler now calls `Api.LogoutAsync()` instead of `AuthState.ClearAuth()` directly → ensures server-side token revocation
- Handler changed from `void` to `async Task` to support async API call

**Pattern:** Components never handle 401 manually — all auth'd API calls now auto-refresh and retry seamlessly. Only refresh failure triggers re-login.

### 2026-03-25: CustomerPortal — 401→Refresh→Retry + MyProfile Edit Page

**CustomerApiClient Transparent Token Refresh (mirrors WarpApiClient):**
- Constructor now takes `CustomerAuthState` and `NavigationManager` alongside `HttpClient`
- `TryRefreshAsync()`: POST /api/auth/refresh (cookie-based), updates `DefaultRequestHeaders.Authorization` and `CustomerAuthState` on success
- `SendWithRefreshAsync(Func<Task<HttpResponseMessage>>)`: Retry wrapper — on 401, attempts refresh; on success retries; on refresh failure clears auth + navigates to `/login`
- `GetMyContactAsync()` and new `UpdateMyContactAsync()` route through `SendWithRefreshAsync`
- `LogoutAsync()`: POST /api/auth/logout then clears bearer header, auth state, navigates to `/`
- Removed `SetAccessToken`, `ClearAccessToken`, `IsAuthenticated`, and stub `RefreshTokenAsync`

**Login.razor (CustomerPortal):**
- Now just calls `AuthState.SetAuth(result)` — no manual bearer header set at login. Cookie from login drives subsequent refresh, matching the Web app pattern.

**NavMenu.razor (CustomerPortal):**
- Injects `CustomerApiClient Api`; Logout is now `async Task` calling `Api.LogoutAsync()`

**MyProfile.razor — Inline Edit:**
- Added `isEditing` bool, `EditProfileModel` inner class, `EnterEditMode`/`CancelEdit`/`SaveChanges` handlers
- Edit mode: form for FirstName, LastName, Phone, JobTitle; Email shown as read-only (auth-linked)
- Save: calls `Api.UpdateMyContactAsync(id, request)` — passes existing values for Status/CompanyId/Email unchanged
- Inline success/error alert banners matching ContactDetail pattern

**Pattern:** CustomerPortal now has full parity with Web for token refresh — all portal API calls are self-healing on 401 via cookie refresh.

### 2026-03-25: Deal List and Detail Pages

**DealList.razor (`/deals`):**
- Paged table: Title (linked), Stage (badge), Value (currency), Close Date, Contact, Company
- Pipeline summary bar at top using `GET /api/deals/summary` — shows TotalPipelineValue + per-stage count/value
- Client-side title filter on current page (debounced, 350ms)
- Pagination matches ContactList pattern (numbered pages + prev/next)
- "New Deal" button → `/deals/new`; title cells link to `/deals/{id}` via plain `<a>` tags

**DealDetail.razor (`/deals/{Id:guid}` + `/deals/new`):**
- Dual `@page` directives; `_isNew = Id == Guid.Empty` detects new-deal mode
- New deal: form shown immediately; POST on save → redirect to `/deals/{newId}`
- Edit: inline toggle; PUT on save; success/error banners
- Delete confirmation modal → `DeleteDealAsync` → redirect to `/deals`
- Fields: Title (required), Stage (dropdown), Value ($), Probability (%), Expected Close Date (date picker)
- View mode: Contact/Company rendered as links if IDs present, else "—"

**WarpApiClient Additions:**
- `GetDealsAsync`, `GetDealAsync`, `CreateDealAsync`, `UpdateDealAsync`, `DeleteDealAsync`, `GetPipelineSummaryAsync`
- Pipeline summary hits `api/deals/summary` (not `api/deals/pipeline` — check DealsController route)
- All use `SendWithRefreshAsync` pattern

### 2026-03-26: Deal List and Detail Pages

**DealList.razor (`/deals`):**
- Paged table: Title (linked), Stage (badge), Value (currency), Close Date, Contact, Company
- Pipeline summary bar at top using `GET /api/deals/summary` — shows TotalPipelineValue + per-stage count/value
- Client-side title filter on current page (debounced, 350ms)
- Pagination matches ContactList pattern (numbered pages + prev/next)
- "New Deal" button → `/deals/new`; title cells link to `/deals/{id}` via plain `<a>` tags

**DealDetail.razor (`/deals/{Id:guid}` + `/deals/new`):**
- Dual `@page` directives; `_isNew = Id == Guid.Empty` detects new-deal mode
- New deal: form shown immediately; POST on save → redirect to `/deals/{newId}`
- Edit: inline toggle; PUT on save; success/error banners
- Delete confirmation modal → `DeleteDealAsync` → redirect to `/deals`
- Fields: Title (required), Stage (dropdown), Value ($), Probability (%), Expected Close Date (date picker)
- View mode: Contact/Company rendered as links if IDs present, else "—"

**WarpApiClient Additions:**
- `GetDealsAsync`, `GetDealAsync`, `CreateDealAsync`, `UpdateDealAsync`, `DeleteDealAsync`, `GetPipelineSummaryAsync`
- Pipeline summary hits `api/deals/summary` (not `api/deals/pipeline` — check DealsController route)
- All use `SendWithRefreshAsync` pattern

**Razor Gotcha:** Can't use `$"..."` string interpolation with escaped quotes (`$\"...\"`) inside `@onclick` attributes in Razor — Razor parser chokes on the backslash escape. Use a plain `<a href="/deals/@id">` anchor instead of `Nav.NavigateTo` in `@onclick`.

### 2026-03-26: Custom Fields UI — Admin Management + Dynamic Contact Forms

**WarpApiClient Additions:**
- `GetCustomFieldDefinitionsAsync(entityType)`: GET /api/custom-fields?entityType=...
- `CreateCustomFieldDefinitionAsync`, `UpdateCustomFieldDefinitionAsync`, `DeleteCustomFieldDefinitionAsync`
- `DeleteCustomFieldDefinitionRawAsync`: returns int status code — used to detect 409 conflict (field has data → deactivate instead)
- All use `SendWithRefreshAsync` pattern

**Components/Shared/CustomFieldInput.razor (new reusable component):**
- Renders appropriate input by `FieldType`: Text → text, Number → number, Date → date, Boolean → checkbox, Select → select dropdown
- `Field.FieldName` as label with `*` for required fields; Bootstrap form classes
- Boolean change extracted to `HandleBoolChange()` method — cannot inline `"true"/"false"` string literals inside Razor `@onchange` attributes (Razor quote gotcha)

**Admin/CustomFieldManagement.razor (/admin/custom-fields):**
- `[Authorize(Roles = "Admin")]` matches UserManagement pattern
- Table: Name, Type badge, Required badge, Display Order, Active toggle, Edit/Delete actions
- Inline create/edit form below table (not modal) — single `_showForm` bool with `_editingId` for edit vs. create branch
- Select field type shows textarea for comma-separated options, parsed via `Split(',', TrimEntries)`
- 409 conflict on delete: message "Field has data — deactivate it instead"
- Toggle Active: quick PUT with `!def.IsActive` directly from table row button

**NavMenu.razor:** Custom Fields link added under Admin section (after Users link)

**ContactDetail.razor extensions:**
- `OnInitializedAsync` now runs `LoadContactAsync()` and `LoadFieldDefinitionsAsync()` in parallel via `Task.WhenAll`
- View mode: Custom Fields section shows fields with a value OR that are required; Boolean rendered as Yes/No
- Edit mode: `<CustomFieldInput>` per active definition, ordered by `DisplayOrder`; `_fieldValues` dict pre-populated from `contact.CustomFields`
- Save: `UpsertCustomFieldValueRequest` list built from `_fieldValues` dict

**CustomerPortal/MyProfile.razor extensions:**
- Inline replicated field type logic (portal doesn't share components with Web)
- `HandlePortalBoolChange(defId, e)` method for boolean toggle (same Razor quote gotcha fix)
- `OnInitializedAsync` loads contact + field defs in parallel
- Save includes custom fields in `UpdateContactRequest`

### 2026-03-26: WarpBusiness.MarketingSite — Static HTML/CSS/JS Advertising Site

**Project Created:** `src/WarpBusiness.MarketingSite/` — standalone marketing site, NOT part of Aspire orchestration.

**Stack:** Pure HTML5, CSS custom properties, vanilla JS (no frameworks), served by minimal ASP.NET Core (`UseDefaultFiles` + `UseStaticFiles`).

**Design:**
- Dark space theme: deep navy/black (`#050b18`) backgrounds, electric blue/cyan (`#00c8ff`) accents
- Google Fonts: Orbitron (headings) + Inter (body)
- Full-viewport hero with animated star-field canvas (vanilla JS, requestAnimationFrame)
- CSS `@keyframes` animations: `fadeDown` (hero reveal), `revealUp` (scroll-in cards), `bounce` (scroll arrow)
- IntersectionObserver scroll-reveal for feature cards, stat numbers, CTA box
- Mobile-first responsive: hamburger nav menu below 768px

**Sections:** Nav → Hero → Features (4 cards) → Stats → CTA → Footer

**Gotcha:** `cta-section__sub` has `margin-bottom` declared twice (once as part of the `margin-inline: auto` shorthand group and once standalone) — functionally OK, last value wins, but worth cleaning if editing the CSS later.

**Added to solution:** `dotnet sln add` to `WarpBusiness.slnx` succeeded. Project targets `net10.0`, `Microsoft.NET.Sdk.Web`.

### 2026-03-27: Rotating Plugin Showcase Added to MarketingSite

**Plugin Carousel:**
- Added `.plugins` section to `index.html` between Features and Stats, with developer reminder HTML comment above it
- Two plugins: CRM (🎯) and Employee Management (👥), each with icon, name, tagline, description, glowing "Available Now" badge
- Auto-rotates every 4 seconds; pauses on hover/focus; dot nav for manual jump; smooth CSS opacity/transform fade transition
- All vanilla JS — plugins array in `main.js` with maintenance comment; `syncTrackHeight()` normalises card container height after paint

**Convention captured:** New plugins must be added to the `plugins` array in `js/main.js`. Sample plugin excluded (developer scaffold only). Decision written to `.squad/decisions/inbox/vasquez-plugin-showcase-reminder.md`.



**NavMenu.razor cleanup:**
- Removed hardcoded `Contacts`, `Companies`, `Deals`, and `Activities` nav links
- These are now contributed dynamically by `CrmModule.GetNavItems()` via `GET /api/modules/nav-items`
- Existing "EXTENSIONS" section renders them at runtime; no markup change needed there
- Home, Admin section (Users, Custom Fields, Modules), and auth footer all preserved

**Router AdditionalAssemblies (Routes.razor):**
- Added `typeof(WarpBusiness.Plugin.Crm.CrmModule).Assembly` to `AdditionalAssemblies`
- Blazor Router will now discover `@page` routes defined in Plugin.Crm when CRM pages eventually move there

**WarpBusiness.Web.csproj:**
- Added `<ProjectReference>` to `WarpBusiness.Plugin.Crm`
- Build succeeds (0 errors); EF Core 10.0.4 vs 10.0.5 MSB3277 warning is a pre-existing Plugin.Crm dependency mismatch, not a frontend concern


