# Bishop: Multi-Tenancy Auth Implementation

**Date:** 2026-03-27  
**Author:** Bishop (Auth Specialist)  
**Status:** Implemented — commit `de3eacd`

---

## What Was Built

Full auth layer for multi-tenancy. All auth surfaces updated to understand and enforce tenant context.

---

## Files Created

| File | Purpose |
|---|---|
| `src/WarpBusiness.Api/Filters/TenantScopeFilter.cs` | `[RequireTenantRouteMatch]` action filter — IDOR guard at tenant boundary |
| `src/WarpBusiness.Api/Middleware/TenantResolutionMiddleware.cs` | Subdomain tenant slug extraction; Phase 2 enforcement (disabled by default) |
| `src/WarpBusiness.Api/Services/ITenantSamlService.cs` | SAML service interface |
| `src/WarpBusiness.Api/Services/TenantSamlService.cs` | SAML service stub (config storage works; auth flow TODO) |

---

## Files Modified

| File | What Changed |
|---|---|
| `AuthController.cs` | Login: tenant-aware token issuance (1 tenant → full, multi → pre-auth). Added `POST /api/auth/select-tenant` and `GET /api/auth/my-tenants` |
| `TenantsController.cs` | Added SAML endpoints (GET/PUT/{tenantId}/saml, POST enable/test). Fixed `GenerateAccessToken` call to pass `tenant_role`. Added `ITenantSamlService` injection |
| `Identity/RefreshToken.cs` | Added `ActiveTenantId` nullable Guid — carries active tenant through token rotation |
| `Identity/TokenService.cs` | Extended `GenerateAccessToken` with `tenant_role` and `allTenantIds` params. Added `GeneratePreAuthToken`. Updated `CreateRefreshTokenAsync` and `ValidateAndRotateRefreshTokenAsync` to carry `ActiveTenantId` |
| `Program.cs` | Added `RequireActiveTenant` and `RequireTenantAdmin` policies. Registered `ITenantSamlService`. Added `UseTenantResolution()` middleware |
| `appsettings.json` | Added `WarpBusiness.RootDomain` and `WarpBusiness.SubdomainRoutingEnabled` config |
| `Shared/Auth/TenantDtos.cs` | Added `MyTenantDto`, `SamlConfigDto`, `SaveSamlConfigRequest` |
| CRM controllers (5) | Changed `[Authorize]` → `[Authorize(Policy = "RequireActiveTenant")]` |
| EmployeeManagement controller | Changed `[Authorize]` → `[Authorize(Policy = "RequireActiveTenant")]` |

---

## Key Design Decisions

### JWT Login Flow
- **0 tenants:** basic token (no tenant claims)
- **1 tenant:** full token with `tenant_id`, `tenant_slug`, `tenant_role`, `tenants[]`
- **2+ tenants:** pre-auth token with only `tenants[]` — client calls `POST /api/auth/select-tenant`

### Token Refresh
`RefreshToken.ActiveTenantId` is set on login and `select-tenant`. On refresh, the service re-looks up the tenant from DB and re-issues with current tenant data (catches membership changes). Falls back to basic token if tenant no longer found.

### Cross-Tenant Guard
`[RequireTenantRouteMatch]` checks `{tenantId}` route param against JWT `tenant_id` claim. Returns 403 if mismatch. Applied to all SAML endpoints. Not applied to `{id}` routes (those use `IsUserInTenant()` helper check in TenantsController).

### TenantClaimsTransformation
No-ops when token already has `tenant_id` (token is authoritative). Auto-resolves for single-tenant users on legacy/basic tokens. This means existing logged-in users get tenant claims enriched without needing to re-login.

### SAML
SAML config storage is fully functional. Authentication flow requires Sustainsys.Saml2 (or equivalent). Add the NuGet reference when ready to implement; the stub's `TestConnectionAsync` documents the TODO.

---

## What's Still Needed

1. **DB Migration** — `RefreshToken.ActiveTenantId` column needs a migration. Create after Hicks's tenant schema migration is finalized.
2. **SAML auth flow** — `TenantSamlService.TestConnectionAsync` is a stub. Add `Sustainsys.Saml2` when ready.
3. **CRM/Employee data layer** — `RequireActiveTenant` policy now enforces JWT tenant claim, but services don't filter by tenant yet. Hicks needs to add `TenantId` to CRM/Employee entities and filter queries.
4. **OIDC tenant mapping** — `ExternalIdentityMapper.EnsureUserAsync` needs to assign the user to a tenant on first OIDC login (if tenant is configured in IdP claims).
5. **Phase 2 subdomain routing** — Flip `WarpBusiness:SubdomainRoutingEnabled: true` when wildcard DNS and TLS are configured.
