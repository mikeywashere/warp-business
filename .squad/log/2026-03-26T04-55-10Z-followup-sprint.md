# Session Log: Follow-Up Sprint Checkpoint

**Date:** 2026-03-26T04:55:10Z  
**Sprint:** Follow-Up Sprint — Portal Refresh + Deal Management + Activities Service  
**Team:** Vasquez, Hicks  

## Checkpoint Summary

This checkpoint consolidates three parallel work streams completed across the follow-up sprint:

### 1. Vasquez — Portal Refresh & Deal Management (2 stories)
- **CustomerPortal 401→Refresh→Retry:** Implemented transparent token refresh matching WarpBusiness.Web patterns. Added `TryRefreshAsync()` wrapper, `SendWithRefreshAsync()` auto-retry, and `LogoutAsync()` for proper server-side revocation. MyProfile page now supports inline edit mode with success/error feedback.
- **Deal Management UI:** Built DealList.razor with paging + pipeline summary bar, DealDetail.razor supporting create/edit/delete/view modes, and 6 WarpApiClient methods. UI uses async/await patterns with proper error handling. All authenticated calls use token refresh wrapper.
- **Status:** ✅ Built clean, pushed. See orchestration log for full detail.

### 2. Hicks — Activity Service (1 story)
- **Activity Service Complete:** Implemented `IActivityService` / `ActivityService` with full async/CancellationToken support. Entity uses computed `IsCompleted` property; DTO mapping derived `CompanyId`/`CompanyName` from Contact/Deal navigations. Created `ActivitiesController` with GET, POST, PUT, DELETE endpoints. All service methods set OwnerId/CreatedBy from JWT claims. EF configuration applied.
- **Status:** ✅ Built clean, pushed (commit fc35103). See orchestration log for full detail.

## Key Learnings

### Token Refresh Pattern (Vasquez)
- Both CustomerPortal and WarpBusiness.Web now use identical refresh flow: 401 → TryRefresh() → Retry once → On failure: clear auth + redirect to /login
- Cookie-based refresh: client never stores refresh token; all refresh calls are POST /api/auth/refresh (no body, auth via cookie)
- NavMenu/Logout call Api.LogoutAsync() for server-side revocation instead of direct auth state clear

### Activity Service Entity Design (Hicks)
- `IsCompleted` is computed from `CompletedAt.HasValue` — not a stored column
- No direct `CompanyId` on Activity; company filtering uses included navigations to Contact.Company or Deal.Company
- EF `.Include().AsNoTracking()` then in-memory projection avoids translation errors on null-conditional navigation chains

### Deal Pipeline Metrics (Vasquez)
- Backend groups deals by Stage; frontend excludes `ClosedLost` from `TotalPipelineValue`
- Pipeline summary endpoint is `GET /api/deals/summary` (placed before `{id:guid}` route to prevent conflicts)
- Stage filter is case-insensitive and silently skips unknown values (no 400 error)

## Decisions Made

1. **Activity Company Derivation:** Company filter on activities goes through Contact.Company or Deal.Company navigations — no denormalization.
2. **Deal New Mode Detection:** Routes `/deals/{id:guid}` and `/deals/new` on same component; `_isNew = Id == Guid.Empty` detects new.
3. **MyProfile Edit Inline:** Single toggle for view/edit mode; EditForm captures FirstName/LastName/Phone/JobTitle; Email shown read-only (auth-linked).

## Next Steps (Not Addressed This Sprint)

- Full OIDC flows for Microsoft provider (currently placeholder with guidance message)
- Activity scheduling/reminders (service structure supports future enhancement)
- Deal probability weighting in pipeline (currently arithmetic sum; could add weighting)
- Company portal role-based filtering (activities, deals filtered to own company only — TBD)

## Build & Verification

- All builds: ✅ Clean
- Tests: ✅ Manual component/API flows verified in browser
- Migrations: ✅ Activity tables included in InitialCreate (fc35103 baseline)
- Pushes: ✅ All code pushed to origin

---

**Created by:** Scribe  
**Agents Covered:** Vasquez (3 learnings appended), Hicks (3 learnings appended)  
**Files Generated:** 3 orchestration logs, this session log  
