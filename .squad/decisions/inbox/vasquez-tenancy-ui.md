# Decision: Tenant UI — Implementation Notes for Team

**Date:** 2026-03-27  
**Author:** Vasquez (Frontend Dev)  
**Status:** Shipped

## Summary

Tenant selection and workspace administration UI is live in `src/WarpBusiness.Web/`.

## Routes Added

| Route | Component | Guard |
|---|---|---|
| `/tenant/select` | `TenantSelect.razor` | Authenticated |
| `/tenant/signup` | `TenantSignup.razor` | Authenticated |
| `/settings/workspace` | `Settings/TenantAdmin.razor` | `TenantAdmin` role |

## API Endpoints Called

| Method | Path | Status | Notes |
|---|---|---|---|
| GET | `/api/auth/my-tenants` | ✅ Implemented (AuthController) | Returns `MyTenantDto` list |
| POST | `/api/auth/select-tenant` | ⚠️ Not yet implemented | Frontend handles failure gracefully (falls through to `/`) |
| POST | `/api/tenants/signup` | ✅ Implemented (TenantsController) | Returns `AccessToken` inline |
| GET | `/api/tenants/{id}` | ✅ Implemented | Returns `TenantDetailDto` incl. members |
| PUT | `/api/tenants/{id}` | ✅ Implemented | 204 NoContent |
| PUT | `/api/tenants/{id}/members/{userId}/role` | ✅ Implemented | `ChangeMemberRoleRequest` |
| DELETE | `/api/tenants/{id}/members/{userId}` | ✅ Implemented | 204 NoContent |
| POST | `/api/tenants/{id}/members` | ✅ Implemented | `AddMemberRequest {email, role}` |
| GET | `/api/tenants/check-slug?slug=...` | ⚠️ Not implemented | UI shows check UI, fails gracefully |

## Pending Backend Work (Hicks)

1. **`POST /api/auth/select-tenant`** — required for multi-tenant users to switch workspaces without re-login. Until this ships, users with multiple tenants will see a "Failed to select workspace" error and stay on the picker page.

## Shared DTO Notes

- `MyTenantDto(Guid Id, string Name, string Slug, string Role)` — used for `/api/auth/my-tenants`
- `TenantDetailDto` — returned by `GET /api/tenants/{id}`, includes members inline
- `TenantMemberDto(string UserId, string Role, DateTimeOffset JoinedAt)` — minimal from API; no Email/FullName yet in API response
- `TenantSignupResponse.AccessToken` — use this directly after signup, no select-tenant call needed

## CSS

Tenant card styles added globally to `app.css`:
- `.tenant-select-wrapper` — centered max-width container
- `.tenant-card` — hover lift + translucent dark background
- `.tenant-avatar` — gradient avatar circle with initial letter
