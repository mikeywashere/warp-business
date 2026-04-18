# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, PostgreSQL, Blazor
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Progressive Streaming + Batch Rendering Pattern (2026-04-17)

- **Integrated workflow:** API streams via `IAsyncEnumerable<T>` endpoints; Blazor UI consumes stream and renders in fixed-size batches (25 items) with live progress counter.
- **Batch rendering benefits:** Initial screen population faster (first 25 items arrive while remaining stream downloads). UI remains responsive during large list loads. No artificial pagination UI needed.
- **Post-mutation reloads:** For add/edit/delete operations, still use bulk endpoint (non-streaming), not incremental append. Reasoning: mutations are rare; load performance only matters on initial/refresh load.
- **Stream consumption:** `CatalogApiClient.GetProductsStreamAsync()` and `TaxonomyApiClient.GetProviderNodesStreamAsync()` handle header check and lazy deserialization.
- **Products.razor implementation:** Loop over stream with `foreach await` (wrapped in streaming context), append batch to list, UI re-renders each batch automatically via Blazor binding.
- **Backward compatibility:** Non-streaming bulk endpoints (`/api/catalog/products`, `/api/taxonomy/providers/{key}/nodes`) remain unchanged; streaming is opt-in via separate routes.

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
- Password field on user creation form has inline policy checklist (8+ chars, uppercase, lowercase, digit, special character) with live ✓/✗ indicators
- Submit button is disabled until password meets all policy requirements (create mode only)
- `ApiException` class in `UserApiClient.cs` parses JSON error bodies (`message`, `detail`, `title` fields) and provides fallback messages per status code
- `UserApiClient.CreateUserAsync` reads response body on failure instead of using `EnsureSuccessStatusCode`, enabling user-friendly error display
- `HandleFormSubmit` catches `ApiException` separately from generic exceptions for cleaner error messages
- Catalog management UI at `/catalog/*` pages (Categories, Products, Colors, Sizes) — all require auth, use modal-based editing, follow consistent table + Actions column pattern
- `CatalogApiClient` service wraps all catalog HTTP operations (categories, colors, sizes, products, variants) — pattern mirrors `UserApiClient`
- Product variants support color + size combinations; variant SKU/price/stock are optional overrides of product defaults
- Category hierarchy supported via `ParentCategoryId` (nullable) — shown with "↳" indent in table
- Actions column header uses `style="width: 250px;"` per team convention for consistent button layout
- Product images: `ImageKey` (string?) field on Product and ProductVariant DTOs stores MinIO object key, not full URL
- Image display URL: `CatalogApiClient.GetImageUrl(imageKey)` returns `{apiBaseUrl}/api/catalog/images/{imageKey}` (proxy endpoint)
- Product thumbnails: 48×48px, shown in product table and variant table with `.product-thumbnail` class
- Image placeholder: emoji 📷 in `.no-image-placeholder` (48×48) or `.no-image-placeholder-sm` (36×36) for variants
- Product modal: image upload section shown only when editing (editingProductId.HasValue), uses Blazor `<InputFile>` component
- File validation: 5MB max size, accept="image/*", enforced in `HandleProductImageUpload` and `HandleVariantImageUpload` methods
- Image upload flow: open stream (5MB limit), call API, update local state (`currentProductImageKey` or refresh variants), show success message
- Variant image management: dedicated modal (`showVariantImageModal`) with thumbnail preview, Upload + Remove buttons
- CSS: `.product-preview-image` (max 300×300px) for larger modal previews, maintains aspect ratio with `object-fit: contain`
- Image methods: `GetProductImageUrl()`, `HandleProductImageUpload()`, `RemoveProductImage()`, `ShowVariantImageUpload()`, `HandleVariantImageUpload()`, `RemoveVariantImage()`
- Variant image button in Actions column: 📷 emoji button with `.btn-outline-info` styling, opens variant image modal
- Video support: Added VideoKey field to Product and ProductVariant DTOs alongside ImageKey
- Video upload methods: `UploadProductVideoAsync()`, `UploadVariantVideoAsync()`, `DeleteProductVideoAsync()`, `DeleteVariantVideoAsync()` in CatalogApiClient
- Video file limit: 500MB max (vs. 5MB for images), validated in upload handlers with `maxAllowedSize: 500L * 1024 * 1024`
- Video display: HTML5 `<video>` tag with `controls` and `preload="metadata"` attributes, shown in product edit modal and variant media modal
- Video upload UI: Separate section below image upload, shows "Uploading video..." spinner during upload (state: `isUploadingVideo`, `isUploadingVariantVideo`)
- Video indicators in tables: 🎬 emoji badge (`.video-badge` or `.video-badge-sm`) shown alongside thumbnails when VideoKey is present
- Video URL helper: `GetProductVideoUrl(videoKey)` returns `{apiBaseUrl}/api/catalog/videos/{videoKey}` (proxy endpoint, like images)
- Video preview styling: `.product-video-preview` class (max 320px width, responsive, dark theme borders)
- Variant media modal renamed conceptually: handles both images and videos (title still says "Variant Image" for brevity, but includes video section)
- Large file upload note: Comment added in CatalogApiClient about API's `MaxResponseContentBufferSize` needing adjustment for large videos (Data's responsibility)
- `TokenValidationParameters.RoleClaimType = "roles"` maps Keycloak's `roles` claim to .NET role claims, enabling `AuthorizeView Roles="..."` in Blazor components
- **Tenant selection on user creation**: Add User form includes required tenant dropdown with type-ahead search; uses `TenantApiClient.GetTenantsAsync()` to populate options
- **Type-ahead pattern**: Use `value="@field"` + `@oninput` (not `@bind` + `@oninput` together) to avoid Blazor RZ10008 duplicate attribute error
- **Dropdown blur handling**: `@onmousedown` with `@onmousedown:preventDefault` on list items + `Task.Delay(200)` in blur handler allows click events to fire before hiding dropdown
- Taxonomy UI (`/catalog/taxonomy`, `/catalog/taxonomy/import`) uses flat MaterializedPath ordering with collapsible tree rows; `TaxonomyApiClient` mirrors CatalogApiClient and includes CRUD/external/import methods with tenant header forwarding
- **CreateUserRequest**: Now includes optional `Guid? TenantId = null` parameter for tenant assignment during user creation

### Shift Replacement Recommendation Engine — Endpoint Available (2026-04-18)

- **Endpoint:** `GET /api/scheduling/schedules/{scheduleId}/shifts/{shiftId}/replacements` — implemented in Data's ShiftReplacementEndpoints.cs
- **Response model:** Array of employee candidates with fields: `EmployeeId`, `EmployeeNumber`, `EmployeeName`, `HoursScheduledThisWeek`, `HoursRemainingBeforeOvertime`, `WouldCauseOvertime`
- **Ranking:** Employees sorted by ascending `HoursScheduledThisWeek` (lowest hours first, best replacement first)
- **Authorization:** Requires `SystemAdministrator` role
- **Business rules:** Conflicted employees (same-date time overlap) are excluded entirely from response; overtime flag indicates if candidate would exceed 40-hour week
- **Ready for Geordi:** UI component (`ShiftReplacementPicker.razor` or similar) can consume this endpoint to populate shift assignment picker. Consider dropdown or filterable list given typical small result set (5–15 employees).

### Multi-Tenant User Onboarding (2026-04-12)

- **Frontend:** Implemented tenant dropdown with type-ahead search in Add User form (UserManagement.razor).
- **Integration:** Integrated TenantApiClient for fetching available tenants, added validation and visual feedback.
- **PR #12:** Merged with backend API support (Data agent).
- **Status:** ✅ Complete and deployed.

### Employee Management UI (2026-04-12)

- **EmployeeApiClient** (`WarpBusiness.Web/Services/EmployeeApiClient.cs`): Typed HttpClient with DTOs (EmployeeResponse, CreateEmployeeRequest, UpdateEmployeeRequest), full CRUD methods, same TokenProvider/CreateRequest pattern as UserApiClient and TenantApiClient.
- **EmployeeManagement.razor** (`/employees`): Full CRUD page inheriting AuthenticatedComponentBase. Table shows Employee #, Name, Email, Department, Job Title, Status (color-coded badges), Type, Hire Date. Add/Edit form with validation. Delete with modal confirmation.
- **NavMenu.razor**: Replaced static "Modules" link with Bootstrap dropdown containing Employees link, wrapped in AuthorizeView for auth-only access.
- **Program.cs**: Registered EmployeeApiClient as typed HttpClient with configureApiClient + AuthTokenHandler.
- **Pattern**: EmploymentStatus/EmploymentType are strings in Web DTOs (API enums serialize to strings). Valid: Active/OnLeave/Terminated/Suspended, FullTime/PartTime/Contract/Intern.
- **PR #16:** Squash-merged to main.
- **Status:** ✅ Complete and deployed.

### Employee-User Account Linking — Frontend (2026-04-12)

- **EmployeeApiClient**: Added `CreateEmployeeWithUserRequest`, `UpdateEmployeeWithUserRequest` DTOs and 4 new methods: `GetUnlinkedUsersAsync()`, `CreateEmployeeWithUserAsync()`, `UpdateEmployeeWithUserAsync()`, `GetEmployeeByUserIdAsync()`
- **UserApiClient**: Added `LinkedEmployeeId` to `UserResponse` record
- **EmployeeManagement.razor**: User Account section with mutual-exclusion UX (link existing user OR create new); combined edit view for linked employees with role field; `?edit={id}` query param deep-link support; "User Account" column with 🔗 badge in table; `EmployeeFormModel` extended with `LinkedUserId`, `CreateNewUser`, `UserRole` fields
- **UserManagement.razor**: Edit button redirects to `/employees?edit={LinkedEmployeeId}` for linked users; "Employee Link" column with 🔗 badge in table
- **Pattern**: Type-ahead dropdown for user search reuses same `@oninput` + `@onmousedown:preventDefault` + blur-delay pattern from tenant dropdown
- **Pattern**: `[SupplyParameterFromQuery]` attribute for reading URL query params in Blazor Server pages
- **Design**: Mutual exclusion between "Link Existing" and "Create New" — selecting one disables/clears the other
- **Branch:** `feature/employee-user-linking`
- **Status:** 🔧 Frontend complete, awaiting backend API endpoints

### Warp Brand Rebrand (2026-04-13)

- **Scope:** Rebranded WarpBusiness.Web Blazor app to match marketing site's dark space theme
- **Favicon:** Copied SVG favicon from marketing site, updated App.razor to reference it
- **Fonts:** Added Google Fonts (Orbitron for headings, Inter for body) and theme-color meta tag
- **CSS:** Introduced Warp design tokens as CSS custom properties; overrode Bootstrap defaults for body, cards, tables, forms, modals, buttons, pagination with dark palette (#050b18 bg, #00c8ff accent, #e8f0fe text)
- **Navbar:** Replaced generic `navbar-dark bg-dark` with branded navbar: inline Warp SVG logo, Orbitron brand text, translucent dark bg with backdrop blur, cyan accent links/dropdowns
- **Home:** Replaced "Hello, world!" placeholder with branded dashboard: Orbitron heading, tagline, three module cards (Employees, Users, Tenants) with dark card styling
- **Layout:** Updated MainLayout.razor.css for dark theme page and main area backgrounds
- **Approach:** Kept Bootstrap for structure/grid/components, overrode colors/fonts only — no C# changes
- **Branch:** `fix/logout-redirect`
- **Commit:** eb9be53
- **Status:** ✅ Complete and pushed

### Branding Alignment Audit (2026-04-13)

- **Finding:** Most Warp branding from prior rebrand was already intact (CSS tokens, favicon, fonts, navbar, home dashboard)
- **Gaps fixed:** ReconnectModal (was white/blue default, now dark theme with cyan accents), Error page (now branded with Orbitron heading), NotFound page (now branded 404 with "Lost in Space" theme)
- **ReconnectModal.razor.css:** Updated background to `--clr-bg-card`, border to `--clr-border`, buttons to `--clr-accent`, spinner to cyan, backdrop darkened
- **Error.razor:** Replaced raw text-danger with branded card layout, removed dev environment text (sensitive info exposure risk)
- **NotFound.razor:** Added 404 heading, description, and "Return to Dashboard" button with Warp styling
- **Status:** ✅ Complete — build passes
### Keycloak Custom Theme — "warp" (2026-04-13)

- **Theme location:** `keycloak/themes/warp/` in repo root — bind-mounted into Keycloak container at `/opt/keycloak/themes/warp/`
- **Scope:** `login` and `account` sub-themes, both using `parent=keycloak` to inherit base templates
- **Login CSS:** `keycloak/themes/warp/login/resources/css/login.css` — full dark-space override matching Web app design tokens (#050b18 bg, #00c8ff accent, Orbitron headings, Inter body)
- **Account CSS:** `keycloak/themes/warp/account/resources/css/account.css` — matching dark theme for user account pages
- **Logo:** `keycloak/themes/warp/login/resources/img/logo.svg` — Warp W icon + "WARP BUSINESS" text in Orbitron font
- **Favicon:** `keycloak/themes/warp/login/resources/img/favicon.svg` — matches Web app favicon
- **AppHost wiring:** `WithBindMount("../keycloak/themes/warp", "/opt/keycloak/themes/warp")` on keycloak resource
- **Realm import:** Added `"loginTheme": "warp"` and `"accountTheme": "warp"` to `warpbusiness-realm.json`
- **Note:** If Keycloak data volume exists from previous runs, delete it or manually select theme in Admin Console → Realm Settings → Themes
- **Pattern:** Keycloak themes use `theme.properties` with `parent=keycloak` + `styles=css/filename.css`
- **Status:** ✅ Complete

### Tenant List UI Fix (2026-04-13)

- **Colspan bug:** PR #23 added a Currency column to the tenant table (6 columns total) but left `colspan="5"` on the members panel and empty-state rows — fixed all three to `colspan="6"`
- **Button spacing:** Delete button was missing `me-1` class that Members and Edit buttons had, causing inconsistent gaps — added `me-1` for uniform spacing
- **Currency column:** Already present from PR #23 (header at line 122, data cell at line 136 using `tenant.PreferredCurrencyCode ?? "—"`)
- **Pattern note:** When adding columns to tables, always audit all `colspan` values in the same `<table>` — easy to miss on expandable/inline panels
- **File:** `WarpBusiness.Web/Components/Pages/TenantManagement.razor`
- **Status:** ✅ Complete

### Login Timeout Field — Tenant Form (2026-04-13)

- **DTOs:** Added `LoginTimeoutMinutes` (int, default 480) to `TenantResponse`, `CreateTenantRequest`, `UpdateTenantRequest` in `TenantApiClient.cs`
- **Form field:** Numeric input "Login Timeout (minutes)" with min=5 validation, placed in `col-md-6` alongside currency field
- **Live duration display:** `FormatDuration()` helper renders human-readable text below the input (e.g., "8 hours", "1 1/2 hours", "1 day") — updates as user types via Blazor binding
- **Table column:** Added "Login Timeout" column to tenant list table (between Currency and Status), displays formatted duration in `text-muted` style
- **Colspan audit:** Updated outer table colspans from 6 → 7 (members panel + empty state); inner members table colspan unchanged at 6
- **Pattern:** `FormatDuration` handles minutes, hours, half-hours, days, and compound durations
- **File:** `WarpBusiness.Web/Components/Pages/TenantManagement.razor`, `WarpBusiness.Web/Services/TenantApiClient.cs`
- **Status:** ✅ Complete — build passes

### Dark Theme Text Visibility Fix (2026-04-13)

- **Problem:** Bootstrap utility classes (`text-muted`, `<code>`, `bg-light`) use light-theme defaults that are unreadable on the Warp dark background (#050b18)
- **Fix:** Added CSS overrides in `app.css` for `code` (→ cyan accent), `.text-muted` (→ --clr-text-muted #8899bb), `.bg-light` (→ dark section bg)
- **Pattern:** Always override Bootstrap utility classes in app.css when they conflict with the dark theme — don't rely on inheritance from `.table` or `.card` rules since utility classes have higher specificity
- **File:** `WarpBusiness.Web/wwwroot/app.css`
- **Status:** ✅ Complete — build passes

### Comprehensive Dark Theme Text Overrides (2026-04-13)

- **Problem:** Previous dark theme fix only covered `code`, `.text-muted`, `.bg-light` — many Bootstrap components still rendered black/dark text (alerts, badges, buttons, table-dark headers, form elements, dropdowns, close buttons, etc.)
- **Fix:** Added ~250 lines of CSS overrides in `app.css` covering all Bootstrap utility classes and components that default to light-theme colors
- **Key overrides:** `.alert-danger/.alert-success/.alert-warning` (translucent dark bg + light text), `.badge` variants, `.btn-secondary` + all `.btn-outline-*` variants, `.btn-close` (inverted filter), `thead.table-dark` (accent-colored), `.form-check-label/.form-check-input/.form-switch`, `.form-select option`, `.text-danger`, `.dropdown-menu/.dropdown-item`, `.breadcrumb`, `.list-group-item`, `.tooltip-inner/.popover`, `.validation-errors`
- **Pattern:** When adding dark theme to a Bootstrap app, audit EVERY utility class and component variant — Bootstrap has dozens of classes that assume white/light backgrounds
- **Pattern:** `.text-dark` intentionally NOT overridden — it's used correctly on `bg-warning` badges where dark text on yellow is the right contrast
- **Branch:** `fix/tenant-text-colors`
- **PR:** #28
- **Status:** ✅ Complete — build passes

### CRM Customer Management UI (2026-04-13)

- **CrmApiClient** (`WarpBusiness.Web/Services/CrmApiClient.cs`): Full-featured typed HttpClient with DTOs (CustomerResponse, CreateCustomerRequest, UpdateCustomerRequest, CustomerEmployeeResponse, AssignEmployeeRequest, UpdateRelationshipRequest), CRUD operations for customers (Get, Create, Update, Activate, Deactivate), customer-employee relationship operations (GetCustomerEmployees, AssignEmployee, UpdateRelationship, UnassignEmployee), GetAvailableEmployees endpoint
- **Customers.razor** (`/crm/customers`): Customer list page inheriting AuthenticatedComponentBase. Table shows Name, Email, Phone, Industry, Company Size, Status (Active/Inactive badges), Actions (Edit, Details, Activate/Deactivate toggle). Add/Edit modal with full form (name, email, phone, address, city, state, postal code, country, website, industry, company size dropdown [1-10, 11-50, 51-200, 201-500, 501+], notes textarea). Search/filter by name or email (live filtering). Activate/Deactivate with confirmation modal.
- **CustomerDetail.razor** (`/crm/customers/{customerId:guid}`): Customer detail page with read-only info card (contact info, address, notes) + employee assignments section. Table shows assigned employees with Name, Email, Relationship, Actions (Edit relationship, Remove). "Assign Employee" button launches modal with type-ahead employee search + relationship text field. Edit Relationship modal with prefilled text field. Unassign confirmation modal. All modals use dark theme styling.
- **NavMenu.razor**: Added "CRM" link to Modules dropdown (alongside Employees)
- **Program.cs**: Registered CrmApiClient as typed HttpClient with configureApiClient + AuthTokenHandler (same pattern as EmployeeApiClient, TenantApiClient, etc.)
- **Pattern**: Employee dropdown reuses same type-ahead pattern from EmployeeManagement.razor (@oninput + @onmousedown:preventDefault + blur-delay)
- **Pattern**: All DTOs mirror backend schema expectations (CustomerResponse includes IsActive bool, CustomerEmployeeResponse includes employee first/last name + email for display)
- **Design**: Dark theme with Warp design tokens (--clr-bg-card, --clr-accent, --clr-border), Bootstrap modals/forms/tables with existing dark overrides
- **Commit:** 48b534f
- **Status:** ✅ Complete — ready for backend API endpoints from Data team

### CRM Currency & Billing Fields (2026-04-13)

- **Schema changes:** Customer.Currency (ISO 4217, required, defaults to "USD"), CustomerEmployee.BillingRate (decimal 18,2, nullable), CustomerEmployee.BillingCurrency (ISO 4217, nullable)
- **Customers.razor:** Added Currency dropdown to customer form (USD/EUR/GBP/CAD/AUD/JPY/CHF/CNY/INR), added Currency column to customer list table (shows ISO code in `<code>` tag), updated CustomerFormModel with Currency field (required, max 3 chars, default "USD"), updated create/update request DTOs to include Currency
- **CustomerDetail.razor:** Display customer currency in info card, added BillingRate and BillingCurrency columns to employee assignments table, updated Assign Employee modal with BillingRate (decimal input) + BillingCurrency dropdown (defaults to customer's currency), updated Edit Relationship modal with billing fields, added `FormatCurrency()` helper (returns currency symbol + formatted amount, e.g., "$50.00"), updated AssignEmployeeFormModel and RelationshipFormModel with BillingRate/BillingCurrency fields
- **CrmApiClient.cs:** Updated CustomerResponse/CreateCustomerRequest/UpdateCustomerRequest with Currency field, updated CustomerEmployeeResponse/AssignEmployeeRequest/UpdateRelationshipRequest with BillingRate and BillingCurrency fields
- **Pattern:** BillingCurrency defaults to customer's currency in assignment modal (UX shortcut for common case)
- **Validation:** BillingRate has Range(0, 999999.99) validation, BillingCurrency MaxLength(3) for ISO 4217 codes
- **Commit:** f3966e1
- **Status:** ✅ Complete — build passes

### CRM Business Management UI (2026-04-13)

- **Business DTOs:** Added `BusinessResponse`, `CreateBusinessRequest`, `UpdateBusinessRequest` to `CrmApiClient.cs` with full address fields, IsActive toggle, CustomerCount
- **Customer-Business Link:** Updated `CustomerResponse` with `BusinessId` and `BusinessName` fields; updated `CreateCustomerRequest` and `UpdateCustomerRequest` with optional `BusinessId` parameter
- **Businesses.razor** (`/crm/businesses`): Full CRUD page inheriting AuthenticatedComponentBase. Table shows Name, Industry, Phone, Website (clickable link icon), City/Country (formatted), Customer Count, Status (Active/Inactive badges), Actions (Edit, Delete). Add/Edit modal with Name (required), Industry, Website, Phone, full address fields (Address, City, State, PostalCode, Country), Notes, IsActive toggle (edit mode only). Search/filter by name (live filtering).
- **Delete logic:** Two-modal pattern — standard confirmation for businesses with CustomerCount == 0, special warning modal for businesses with linked customers showing "Unlink & Delete" and "Cancel" buttons. Unlink & Delete calls `DeleteBusinessAsync(id, unlinkCustomers: true)`.
- **CrmApiClient methods:** `GetBusinessesAsync()`, `GetBusinessAsync(Guid)`, `CreateBusinessAsync(request)`, `UpdateBusinessAsync(Guid, request)`, `DeleteBusinessAsync(Guid, bool unlinkCustomers)` — follows exact same pattern as Customer methods (CreateRequest + Bearer token + error handling)
- **NavMenu.razor:** Updated Modules dropdown to show "Customers" and "Businesses" separately (was "CRM" link before)
- **Home.razor:** Split CRM card into "Customers" (🤝) and "Businesses" (🏢) cards with distinct descriptions
- **Pattern:** Website column uses link icon (🔗) with `target="_blank"` instead of showing full URL
- **Pattern:** City/Country column uses `string.Join(", ", location)` to format combined location from nullable fields
- **Status:** ✅ Complete — build passes, ready for backend API endpoints from Data team


## Learnings - TenantPortal UI (2026-04-12)

- TenantPortal is a separate Blazor Server project at `WarpBusiness.TenantPortal/` for tenant self-service (modeled after CustomerPortal pattern)
- Uses OIDC client ID `warpbusiness-tenant-portal` in Keycloak (distinct from `warpbusiness-web` and `warpbusiness-customer-portal`)
- `TenantPortalApiClient` provides methods: GetTenant, UpdateSubscription, UploadLogo, DeleteLogo, GetRequests, CreateRequest, CancelRequest
- Logo upload uses base64-encoded data URIs (secure in-memory approach, no temp file storage) via `InputFile` component
- Requests page has full filter bar (search, status dropdown, type dropdown) and inline expandable detail panels
- Status badges color-coded: Open=blue, InProgress=warning, Pending=secondary, Resolved=success, Closed=secondary, Cancelled=dark
- Actions column width set to 250px (per CSS rules) for all tables
- Subscription page shows current plan + update form with MaxUsers, SubscriptionPlan (Starter/Professional/Enterprise), EnabledFeatures
- Signup page is public (`[AllowAnonymous]`) with auto-generated slug from company name (lowercase, hyphens, sanitized)
- Uses reflection (`GetType().GetProperty()`) to access dynamic TenantResponse properties (LogoBase64, LogoMimeType, MaxUsers, etc.) since DTOs may be extended by Data
- Dark theme CSS copied from `WarpBusiness.Web/wwwroot/app.css` (all custom properties: --clr-bg, --clr-accent, --clr-text, etc.)
- Dashboard has card-link navigation to Subscription (📦), Requests (🎫), Logo (🖼️), Profile Info (ℹ️)
- MainLayout navbar shows "⚡ TENANT PORTAL" brand and links to Dashboard, Requests, Logo, Subscription, Logout
- Cancel request action uses JS interop (`IJSRuntime.InvokeAsync<bool>("confirm", ...)`) for confirmation dialogs
- All pages follow consistent card-based layout, loading spinner, error/success alert pattern
## Learnings

### Warnings → Notations Rename + Bootstrap Icons (2026-04-15)

- **Renamed throughout frontend:** "Warnings" → "Notations" (page title, nav menu, API DTOs, route /catalog/warnings → /catalog/notations, form labels, table headers, variable names)
- **Bootstrap Icons integrated:** Added CDN link to App.razor (https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css)
- **Icon dropdown:** Replaced free-text Icon field with <select> dropdown showing 15 curated NotationIcon enum values (Warning, Info, Note, Caution, Danger, Prohibited, Flammable, Chemical, ElectricalHazard, Recyclable, EcoFriendly, FoodAllergen, Prop65, Compliance, Temperature)
- **Icon preview:** Live icon preview next to dropdown in form (uses GetIconClass() helper to map enum names to Bootstrap Icons CSS classes like i-exclamation-triangle-fill)
- **Table icons:** Table displays Bootstrap Icons instead of text/emoji (<i class="bi @GetIconClass(n.Icon)"></i>)
- **Product checkboxes:** Notation checkboxes in product form show icon + name (e.g., <i class="bi bi-fire me-1"></i> Flammable)
- **GetIconClass helper:** Static method mapping enum string values to Bootstrap Icons classes — placed in Notations.razor, Products.razor, and ProductPreview.razor (could be extracted to shared service)
- **Template data:** BuildPreviewTemplateData and BuildTemplateData now pass iconClass alongside icon for each notation in Handlebars context
- **Handlebars template:** Updated default.hbs to use {{#if iconClass}}<i class="bi {{iconClass}}"></i>{{/if}} instead of raw {{icon}} text, renamed CSS classes (.warnings-list → .notations-list, .warning-item → .notation-item), section title "Warnings & Notices" → "Notations & Notices"
- **API client DTOs:** Renamed all CatalogWarningResponse → CatalogNotationResponse, CatalogProductWarningResponse → CatalogProductNotationResponse, CreateCatalogWarningRequest → CreateCatalogNotationRequest, UpdateCatalogWarningRequest → UpdateCatalogNotationRequest
- **API client methods:** Renamed all methods (GetWarningsAsync → GetNotationsAsync, CreateWarningAsync → CreateNotationAsync, UpdateWarningAsync → UpdateNotationAsync, DeleteWarningAsync → DeleteNotationAsync, AddProductWarningAsync → AddProductNotationAsync, RemoveProductWarningAsync → RemoveProductNotationAsync)
- **API routes updated:** All methods now call /api/catalog/notations endpoints (was /api/catalog/warnings), product notation routes /api/catalog/products/{id}/notations/{id} (was /warnings)
- **Products.razor changes:** llWarnings → llNotations, SelectedWarningIds → SelectedNotationIds, ToggleWarning → ToggleNotation, LoadWarnings → LoadNotations, hasWarnings → hasNotations, product.warnings → product.notations in template data
- **ProductPreview.razor changes:** hasWarnings → hasNotations, product.warnings → product.notations in template data, added GetIconClass helper
- **NavMenu.razor:** Link text "Warnings" → "Notations", href /catalog/warnings → /catalog/notations
- **Old file cleanup:** Deleted WarpBusiness.Web/Components/Pages/Catalog/Warnings.razor (replaced by Notations.razor)
- **Build status:** Changes compile successfully (build failed due to running WarpBusiness.Web process holding file locks, not code errors)
- **Pattern:** For icon-based enums, pass both the enum string value AND the CSS class to Handlebars templates since templates can't map enums to classes
- **Status:** ✅ Complete — ready for backend API rename (Data agent responsibility)

## 2026-04-16 — Notations Rename

**Timestamp:** 2026-04-16T04:47:14Z

Completed full frontend Warning→Notation rename:
- Renamed Warnings.razor→Notations.razor (/catalog/notations)
- Added Bootstrap Icons v1.11.3 CDN integration
- Icon field: free-text input → curated dropdown (15 icons with live preview)
- Updated NavMenu.razor, Products.razor, ProductPreview.razor
- Updated CatalogApiClient (routes, DTOs)
- Updated default.hbs template with notations section

**Build:** ✅ 0 errors, 5 pre-existing warnings

**Design notes:** Curated dropdown prevents typos; live preview improves UX. Bootstrap Icons CDN provides zero-footprint solution.

### Taxonomy UI — Branch Picker + Cascade Delete (2026-04-16)

- **TaxonomyApiClient.cs:** Renamed `ImportNodesRequest.TargetParentId` → `TargetParentNodeId` (matches backend API update); added `DeleteBranchResult(Success, ErrorMessage, ConflictingNodeIds)` record; added `GetRootNodesAsync()` (GET /api/taxonomy/nodes/roots), `GetNodeChildrenAsync(Guid)` (GET /api/taxonomy/nodes/{id}/children), `DeleteBranchAsync(Guid, bool cascade)` (DELETE /api/taxonomy/nodes/{id}?cascade=...)
- **DeleteBranchAsync pattern:** Returns `DeleteBranchResult` instead of throwing — 409 sets `ConflictingNodeIds` to empty list (never null), 400 leaves it null; caller uses `ConflictingNodeIds != null` to distinguish conflict vs bad request
- **TaxonomyImport.razor:** Replaced flat "Import into..." dropdown with lazy-loading tree picker in a card section above the Import Selected button; radio toggle (root / branch); picker uses `GetRootNodesAsync` on first expand, `GetNodeChildrenAsync` on chevron click; `@onclick:stopPropagation` on chevron prevents accidental node selection; selected node highlighted with accent border + semi-transparent background; 300px scrollable container; `PickerPaddingLeft(level)` helper uses `InvariantCulture` to prevent locale-specific decimal separators breaking CSS rem values
- **Taxonomy.razor:** Replaced leaf-only "Delete" (disabled for nodes with children) with universal "Delete Branch" (cascade, always enabled); confirmation modal with permanent-action checkbox (must be checked to enable confirm); 409 → specific "catalog references" error; 400 → response body text; success → optimistic subtree removal without full reload
- **Subtree removal:** `RemoveNodeAndDescendants` + `CollectDescendants` use ParentNodeId-based DFS traversal (avoids reliance on MaterializedPath format); also prunes `collapsedNodes` to prevent stale state
- **Toast system:** `ToastEntry(Guid Id, string Text, string Type)` records in `toastMessages` list; fixed bottom-right div; `ShowToastAsync` uses `_ = ShowToastAsync(...)` fire-and-forget + `InvokeAsync(StateHasChanged)` for thread-safe Blazor Server updates; 4s auto-dismiss
- **Commit:** 96e00cb
- **Build:** ✅ 0 errors

### Progressive Streaming — Products Page (2026-04-16)

- **Pattern:** `OnAfterRenderAsync(firstRender: true)` is the correct hook for streaming initial data loads in Blazor Server — `OnInitializedAsync` (via `OnAuthenticatedInitializedAsync`) runs before the circuit is interactive, so it can't trigger incremental re-renders; `OnAfterRenderAsync` fires on the live SignalR connection and allows `StateHasChanged()` to update the DOM progressively
- **Streaming client method:** `CatalogApiClient.GetProductsStreamAsync()` (added by Data agent) — hits `api/catalog/products/stream`, uses `HttpCompletionOption.ResponseHeadersRead` + `JsonSerializer.DeserializeAsyncEnumerable` to yield items as they arrive from the API
- **Batch size = 25:** Balances progressive feel against re-render cost; smaller batches increase renders, larger feel less progressive
- **`await Task.Delay(1)` pattern:** Yields control back to the Blazor Server render loop between batches — essential, otherwise the SignalR message queue fills before the browser renders anything
- **Two-phase loading state:** `isLoading = true` (spinner + count) until first batch of 25 arrives; then `isLoading = false` reveals the table, `isStreaming = true` shows inline "Loading… (N products so far)" banner above table until all items arrive
- **`loadedCount` counter:** Incremented per-item (not per-batch) so the counter in the spinner/banner reflects actual product count, not batch count
- **Post-mutation refreshes unchanged:** `LoadProducts()` still calls `GetProductsAsync()` (bulk endpoint) for fast refresh after create/update/delete — streaming overhead not worth it there
- **Categories + Notations:** Remain in `OnAuthenticatedInitializedAsync` (small lists, synchronous feel preferred)
- **InvokeAsync(StateHasChanged):** Used in finally block and error handler because `OnAfterRenderAsync` may resume on a threadpool thread; `InvokeAsync` marshals back to the Blazor dispatcher
- **File:** `WarpBusiness.Web/Components/Pages/Catalog/Products.razor`, `WarpBusiness.Web/Services/CatalogApiClient.cs`
- **Build:** ✅ 0 errors
