# Project Decisions

## Active Decisions

### 1. Persistent Auth Sessions & LoginTimeoutMinutes

**Date:** 2026-04-13  
**Author:** Data (Backend Dev)  
**Status:** Active

#### Context
Users were being forced to re-login when returning to the site because auth cookies expired and access tokens were not refreshed. The team also needed a per-tenant configurable login timeout.

#### Decision

**Auth Cookie — Sliding Expiration (8 hours default)**
- `.AddCookie()` in `WarpBusiness.Web/Program.cs`:
  - `ExpireTimeSpan = 8 hours` — cookie expires 8 hours after last activity
  - `SlidingExpiration = true` — each request resets the timer
  - `Cookie.MaxAge = 8 hours` — browser discards after 8 hours inactivity
- Users stay logged in as long as they visit within 8 hours

**Keycloak Session Timeouts**
- Added to `warpbusiness-realm.json`:
  - `ssoSessionIdleTimeout: 28800` (8 hours)
  - `ssoSessionMaxLifespan: 28800` (8 hours)
  - `accessTokenLifespan: 300` (5 minutes)
  - `offlineSessionIdleTimeout: 2592000` (30 days)
  - `offlineSessionMaxLifespan: 2592000` (30 days)

**Offline Access Scope**
- Added `offline_access` to OIDC scopes
- Requests offline refresh tokens from Keycloak
- Allows token refresh even after SSO session timeout

**LoginTimeoutMinutes on Tenant**
- New nullable `int` column, default 480 (8 hours)
- Exposed in all tenant DTOs and CRUD endpoints
- Available for frontend to read per-tenant timeout in future

#### Consequences
- ✅ Users stay logged in across browser sessions within 8-hour idle window
- ✅ Access tokens refresh silently (5-minute lifetime, proactive refresh via TokenRefreshService)
- ✅ Per-tenant timeout stored and queryable
- ⚠️ Keycloak data volume must be deleted for realm JSON changes to apply
- ⚠️ 8-hour default hardcoded in cookie config; future work could read from tenant's LoginTimeoutMinutes dynamically

---

### 2. Keycloak Custom Theme ("warp")

**Date:** 2026-04-13  
**Author:** Geordi (Frontend Dev)  
**Status:** Proposed

#### Context
Keycloak's default login/account pages use generic light theme mismatched with Warp Business dark branding, creating jarring UX break between app and auth pages.

#### Decision
Created custom Keycloak theme "warp" matching Warp Business dark space branding:
- **Location:** `keycloak/themes/warp/` (root)
- **Sub-themes:** `login` and `account`, extending `parent=keycloak`
- **Design tokens:** Same as Web app — `#050b18` background, `#00c8ff` cyan accent, Orbitron headings, Inter body text
- **Wiring:** Bind-mounted via `.WithBindMount()` in AppHost.cs; realm import sets `loginTheme` and `accountTheme` to "warp"
- **Assets:** Custom logo SVG (W icon + brand text), favicon matching Web app

#### Consequences
- ✅ Seamless visual transition between Blazor app and Keycloak auth pages
- ✅ Zero template customization — CSS-only overrides on Keycloak defaults
- ✅ Auto-applied on fresh realm provisioning
- ⚠️ Existing Keycloak data volumes won't pick up theme automatically
- ⚠️ Google Fonts loaded from CDN; consider self-hosting in production

---

### 3. Login Timeout Field in Tenant DTOs

**Date:** 2026-04-13  
**Author:** Geordi (Frontend Dev)  
**Status:** Active

#### Context
Login Timeout feature requested for tenant configuration. Frontend needs to send and receive `LoginTimeoutMinutes` on all tenant CRUD operations.

#### Decision
- `TenantResponse`, `CreateTenantRequest`, `UpdateTenantRequest` in `WarpBusiness.Web/Services/TenantApiClient.cs` now include `int LoginTimeoutMinutes = 480`
- Tenant add/edit form includes numeric input with min=5 validation
- Live human-readable duration display (hours and minutes)
- Tenant list table shows formatted timeout value

#### Impact
- **Data (Backend):** API must accept/return `LoginTimeoutMinutes` on tenant endpoints, default 480, persisted in database
- **Worf (Testing):** New form field and table column need E2E coverage

---

### 4. Warp Branding Alignment Complete

**Date:** 2026-04-13  
**Author:** Geordi (Frontend Dev)  
**Status:** Active

#### Context
Align WarpBusiness.Web branding with WarpBusiness.MarketingSite. Prior rebrand (commit eb9be53) was mostly intact; only isolated components needed updates.

#### Decision
All Warp branding CSS custom properties (defined in `app.css`) are the single source of truth:
- `--clr-bg`, `--clr-accent`, `--font-heading`, etc.
- New components must reference these variables instead of hardcoding colors

#### Changes Made
- **ReconnectModal:** Converted to dark Warp theme (dark card background, cyan accent buttons/spinner, darkened backdrop)
- **Error page:** Replaced default with branded layout; removed development-mode sensitive info
- **NotFound page:** Added branded 404 with Orbitron heading, descriptive text, dashboard link

#### Consequences
- ✅ Consistent visual theme across all pages and modals
- ✅ Easier future theme updates via CSS variables
- ⚠️ Developers must remember to use CSS variables for new components

---

### 5. Shift Replacement Endpoint Placement and Cross-Context Strategy

**Date:** 2026-04-18  
**Author:** Data (Backend Dev)  
**Status:** Implemented

#### Context
The shift replacement recommendation engine (`GET /api/scheduling/schedules/{scheduleId}/shifts/{shiftId}/replacements`) requires data from two separate EF Core DbContexts:
- `SchedulingDbContext` — for `EmployeePositions`, `ScheduleShifts`, and `Schedules`
- `EmployeeDbContext` — for employee name/number details

#### Decision

**Endpoint Placement:**  
Placed in `WarpBusiness.Api/Endpoints/ShiftReplacementEndpoints.cs`, not in `WarpBusiness.Scheduling/Endpoints/`, because:
1. `WarpBusiness.Scheduling` has no reference to `EmployeeDbContext` and should not gain one (separation of concerns)
2. `WarpBusiness.Api` already depends on both contexts and is the established home for cross-context endpoints (see `EmployeePortalEndpoints.cs`, `EmployeeUserEndpoints.cs`)

**Cross-Context Join Strategy:**  
Perform a **two-query in-memory join**:
1. Query `SchedulingDbContext.EmployeePositions` for qualified employee IDs (Guid list)
2. Query `EmployeeDbContext.Employees` filtered by that ID list
3. Join result sets in memory using `Dictionary<Guid, Employee>`

This is the established pattern in this codebase. No raw SQL, no shared DB connection tricks, no cross-context EF navigation.

**Overtime / Hours Calculation Scope:**  
"Hours scheduled this week" includes **all shifts across all schedules** for the employee in the Monday–Sunday window containing the target shift's date. This is intentional — overtime is a real-world concern regardless of which schedule generated the shift.

**Conflict Detection:**  
A conflict is defined as any existing shift for the candidate employee on the **same date** whose scheduled time range **overlaps** the target shift: `startA < endB && endA > startB`. Conflicted employees are excluded entirely from the response (not flagged-and-returned).

**Authorization:**  
Uses the `"SystemAdministrator"` authorization policy, consistent with all other scheduling management endpoints.

#### Consequences
- ✅ Endpoint placed at API layer (established pattern)
- ✅ Two-context join implemented in-memory (no architectural coupling)
- ✅ Business rules (overtime, conflicts, ranking) codified
- ✅ Build: 0 errors, 0 warnings
- ⚠️ Weekly hours calculation includes all schedules — verify this behavior matches scheduling policy (anticipated and intentional)

---

### 6. Override Bootstrap Utility Classes for Dark Theme

**Date:** 2026-04-13  
**Author:** Geordi (Frontend Dev)  
**Status:** Active

#### Context

Bootstrap utility classes like `.text-muted`, `<code>`, and `.bg-light` use hard-coded light-theme colors that are unreadable on our dark background (#050b18). Even though we override `.table`, `.card`, and `.form-control` styles, Bootstrap utility classes have higher specificity and override inherited colors.

#### Decision

Override Bootstrap utility classes globally in `app.css` to use Warp design tokens:
- `code` → cyan accent color with subtle background
- `.text-muted` → `--clr-text-muted` (#8899bb) with `!important`
- `.bg-light` → `--clr-bg-section` with light text
- All `.alert-*`, `.badge-*`, `.btn-*` variants with proper dark theme colors
- Table, form, dropdown, breadcrumb, list group, tooltip, popover, modal, and card utilities overridden

#### Consequences

- ✅ All pages automatically get readable text without per-component fixes
- ✅ Design tokens are the single source of truth for colors
- ✅ 253-line override applied to `app.css` covering all major Bootstrap components
- ⚠️ `!important` on utility overrides needed to beat Bootstrap's specificity — acceptable for a global theme override
- ⚠️ Any new Bootstrap utility classes used in future components should be audited against the dark theme

---

### 7. Calendar API Endpoint Design

**Date:** 2026-04-19  
**Author:** Data (Backend Dev)  
**Status:** Active

#### Context
Added `GET /api/scheduling/calendar` to support calendar views in the scheduling UI. Also updated the employee portal schedule endpoint to accept flexible date ranges instead of a hardcoded 28-day window.

#### Decisions

**Calendar endpoint uses SystemAdministrator policy (not a raw `[Authorize(Roles="admin")]`)**  
Consistent with all other admin scheduling endpoints (ShiftReplacementEndpoints, etc.) which use `.RequireAuthorization("SystemAdministrator")`. The `SystemAdministrator` policy has multi-claim logic (handles both `roles` and `app_role` claims from Keycloak) and should always be used instead of raw role strings.

**90-day cap on calendar range**  
Prevents unbounded queries against shifts. Portal schedule endpoint uses a separate 93-day cap (3 calendar months) since portal users have a slightly different usage pattern.

**Cross-context merge is done in memory**  
ScheduleShifts are loaded with `.Include(s => s.Schedule)` for the Schedule.Name and Schedule.TenantId. Positions and employees are queried separately, then merged via `Dictionary<Guid, T>` lookups. This is the established pattern across ShiftReplacementEndpoints and EmployeePortalEndpoints — no cross-context EF navigation.

**CalendarShiftResponse record lives in the endpoint file**  
Not in a separate models file. Consistent with how all other endpoint response records are co-located in their endpoint files in this codebase.

**EmployeePortalApiClient.GetScheduleAsync stays backward-compatible**  
Existing callers that call `GetScheduleAsync()` with no args still work — defaults to (null, null) which omits the query string, and the API defaults to today+28 days.

#### Consequences
- ✅ Calendar endpoint follows established authorization, date-range, and cross-context patterns.
- ✅ Portal schedule is now flexible — supports weekly, monthly, and custom views.
- ✅ No breaking changes to existing callers.
- ⚠️ Shifts referencing employees or positions not found in their respective contexts are silently excluded from results (consistent with replacement candidates endpoint behavior).

---

### 8. Calendar UI — Admin & Employee Portal

**Date:** 2026-04-25  
**Author:** Geordi (Frontend Dev)  
**Status:** Active

#### Context
The scheduling module had a schedule builder and schedule list pages but no cross-schedule calendar view. Admins needed a way to see all published shifts across all schedules in a month layout. Employees needed a calendar alternative to the existing list view on the My Schedule portal page.

#### Decisions

**Admin Calendar (`/scheduling/calendar`)**
- **New page:** `WarpBusiness.Web/Components/Pages/Scheduling/ScheduleCalendar.razor`
- **Route:** `@page "/scheduling/calendar"`
- **Auth pattern:** Same as `ScheduleBuilder.razor` — `@inherits AuthenticatedComponentBase`, `@rendermode InteractiveServer`, `@attribute [Authorize]`, `<AuthorizeView>` wrapper, load in `OnAfterRenderAsync(firstRender)`.
- **Data source:** `SchedulingApiClient.GetCalendarAsync(firstDay, lastDay)` returns `List<CalendarShiftResponse>`.
- **Calendar grid:** CSS Grid `repeat(7, 1fr)` with Sun–Sat headers.
- **Shift chips:** `border-left: 3px solid {PositionColor}`, `rgba(255,255,255,0.05)` background, show `{StartTime}–{EndTime} · {FirstName} {LastName}`, `title` attribute with schedule+position name.
- **Overflow:** First 3 shifts shown; `+N more` badge for remainder (no click action yet).
- **Today highlight:** `outline: 2px solid var(--clr-accent); background: rgba(0,200,255,0.05)`.
- **Nav link:** Added "📅 Calendar" as first item in the Scheduling dropdown in `NavMenu.razor`.

**Employee Portal Calendar (`/schedule` — `MySchedule.razor`)**
- **Toggle:** `btn-group btn-group-sm` at top-right with ☰ List and 📅 Calendar buttons.
- **Default view:** `viewMode = "calendar"` — calendar opens by default.
- **Lazy list loading:** List data only fetched when user first switches to list view.
- **Calendar data:** Separate `calendarShifts` list from `Api.GetScheduleAsync(from, to)` with optional date params.
- **Shift chips:** Status-color-tinted background (no `PositionColor` in portal response).
- **Month navigation:** `PrevCalendarMonth` / `NextCalendarMonth` reload `calendarShifts` for the new month.
- **Two loading states:** `isLoading` (initial full-page) and `calendarLoading` (month nav spinner inside calendar view).

#### Consequences
- ✅ Admins can view all shifts across all published schedules in a unified month calendar
- ✅ Employees get a visual calendar as the default view with easy month navigation
- ✅ Portal list view is preserved unchanged; lazy-loaded on first switch
- ✅ Build: 0 errors, 0 warnings
- ⚠️ Admin calendar chips are truncated for dense days — "+N more" badge provides count but no drill-down yet
- ⚠️ Portal calendar shows no position name/color (not in `PortalShiftResponse`) — status color used as substitute
