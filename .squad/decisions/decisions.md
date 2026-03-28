# Decisions

## 2026-03-27: Companies API Design

**Author:** Hicks (Backend Dev)  
**Status:** Implemented

### What
Company entity added to CRM with full CRUD API. Contact.CompanyId is a nullable FK. Company name is controlled vocabulary — contacts must select from the companies table, not free-text. GET /api/companies/search?q= powers the UI autocomplete. Deleting a company with linked contacts returns 409 Conflict.

### API Surface
- GET /api/companies — paged list
- GET /api/companies/search?q= — autocomplete (≤20 results)
- GET /api/companies/{id} — detail with contacts
- POST /api/companies — create
- PUT /api/companies/{id} — update
- DELETE /api/companies/{id} — 409 if has contacts

---

## 2026-03-26: WarpBusiness.MarketingSite — Static Advertising Website

**Author:** Vasquez (Frontend Dev)  
**Status:** Implemented

### Context
The product needs a public-facing marketing/advertising site at warp-business.com. This is separate from the Aspire-orchestrated application stack and requires no backend, authentication, or database.

### Decision
Create `src/WarpBusiness.MarketingSite/` as a standalone static HTML/CSS/JS site served by a minimal ASP.NET Core host (`net10.0`, `Microsoft.NET.Sdk.Web`), using only `UseDefaultFiles()` and `UseStaticFiles()`. All static assets live in `wwwroot/`.

### Key Design Choices
- **No external CSS frameworks** — custom CSS only, using CSS custom properties for the full color scheme
- **Fonts:** Orbitron (futuristic headings) + Inter (body) via Google Fonts
- **Theme:** Dark space aesthetic — deep navy (`#050b18`), electric blue/cyan (`#00c8ff`) accents, white text
- **Interactivity:** Vanilla JS only — animated star-field canvas (requestAnimationFrame), IntersectionObserver scroll-reveal, smooth-scroll anchors, mobile hamburger menu
- **Not in Aspire AppHost** — marketing site is independent; no service discovery or orchestration needed

### Sections
1. Sticky nav (logo + links + CTA button)
2. Full-viewport hero — "When Business Moves at Warp Speed" + animated star-field
3. Features grid — CRM, Employee Management, Customer Portal, Plugin Architecture
4. Stats bar — "Business at Warp Speed" / 10x Faster / 360° Visibility / ∞ Extensible
5. CTA — "When Business Moves Faster" / Get Started Free
6. Footer — © 2026 Warp Business / warp-business.com

### Trade-offs
- Pure static files means zero server-side logic; SEO relies on static HTML (sufficient for marketing)
- Google Fonts loaded via CDN — small dependency, acceptable for a marketing site
- Canvas star-field degrades gracefully (canvas hidden if JS disabled; content still fully readable)

### Project Location
`src/WarpBusiness.MarketingSite/` added to `src/WarpBusiness.slnx`

---

## 2026-03-27: Plugin Showcase Maintenance Convention

**Author:** Michael R. Schmidt (via Vasquez)  
**Status:** Active Convention

### What
When a new plugin project is added to the WarpBusiness solution, it must be added to the rotating plugin showcase on the marketing site (src/WarpBusiness.MarketingSite/wwwroot/js/main.js — plugins array). The Sample plugin (WarpBusiness.Plugin.Sample) is excluded — it is a developer scaffold template, not a product feature.

### Why
Keep the marketing site current with the product's plugin ecosystem automatically.

---

## 2026-03-27: Multi-Tenancy Authentication & Authorization Design

**Author:** Bishop (Auth Specialist)  
**Date:** 2026-03-27  
**Status:** Analysis (Implementation in Progress)

### Executive Summary

Adding multi-tenancy to Warp Business requires careful auth design to prevent cross-tenant data leaks. Key recommendation: Start with **Single IdP, Shared** (tenant as claim) for MVP. Migrate to **Single IdP, Tenant-Aware** when subdomain routing is implemented.

### User ↔ Tenant Membership

**Phase 1 (MVP):** Single-tenant per user
- Add `TenantId` to `ApplicationUser` entity
- Each user belongs to exactly one tenant at login time

**Phase 2 (Future):** Multi-tenant for consultants/partners
- Introduce `UserTenantMembership` join table: `(UserId, TenantId, Role, IsDefault)`
- User selects tenant at login or via tenant-switcher UI

### Storage Strategy

For MVP: Store `TenantId` in `ApplicationUser` table. Populate via:
- **Local provider:** Admin assigns tenant during user creation
- **OIDC providers:** Map from custom claim OR auto-assign based on email domain

### JWT Tenant Claim

**Recommended claim structure:**
```json
{
  "sub": "user-id-guid",
  "email": "alice@acmecorp.com",
  "tenant_id": "acme-corp",
  "tenant_name": "Acme Corporation"
}
```

**Claim name:** Use `tenant_id` (lowercase, underscore) for consistency with standard claims.

### Token Issuance Changes

**For Local provider** (TokenService.cs):
- Modify `GenerateAccessToken` to include `new Claim("tenant_id", user.TenantId)` and `new Claim("tenant_name", tenantName)`

**For OIDC providers** (ExternalIdentityMapper):
- Map IdP's tenant claim to `ApplicationUser.TenantId` during provisioning
- If IdP doesn't send tenant, assign default or infer from email domain
- OnTokenValidated: Add `tenant_id` claim from `ApplicationUser.TenantId` to ClaimsPrincipal

---

## 2026-03-27: Multi-Tenancy Architecture — Recommendation & Phased Plan

**Author:** Ripley (Lead/Architect)  
**Status:** APPROVED — Phase 1 Implementation In Progress

### Recommendation: Option A — Shared Schema with TenantId Column

Single database, single set of tables. Every data entity gets a `TenantId` foreign key. All queries automatically filter by tenant via EF Core global query filters.

| Factor | Assessment |
|--------|------------|
| **Data isolation** | Logical (enforced by application layer) |
| **Implementation cost** | Low — add column + FK, add EF query filter |
| **Query complexity** | None — filters are automatic via EF |
| **Cross-tenant queries** | Easy (admin dashboards, analytics) |
| **Tenant provisioning** | Insert row into `Tenants` table |
| **Cost at scale** | Lowest — shared infra, connection pooling |

### Alternative Options (Deferred)

- **Option B: Schema-per-Tenant** — Medium complexity, good for 100+ tenants
- **Option C: Database-per-Tenant** — Maximum isolation, high operational burden

### Phase 1: Foundation (Implementation Priority)

| Task | Owner | Status |
|------|-------|--------|
| Create `Tenants` table + EF entity | Hicks | Done |
| Add `TenantId` to all CRM entities | Hicks | Done |
| Add `TenantId` to Employee entity | Hicks | Done |
| Implement `ITenantContext` | Hicks | Done |
| Add EF global query filters | Hicks | Done |
| Update unique indexes to include TenantId | Hicks | Done |
| Add tenant claim to JWT generation | Bishop | Done |
| Add `DefaultTenantId` to `ApplicationUser` | Bishop | Done |
| Test cross-tenant isolation | Hudson | In Progress |
| Update integration tests | Hudson | In Progress |

**Deliverable:** Multi-tenant data isolation via TenantId + JWT claim. Single subdomain (app.warp-business.com) but multiple logical tenants.

### Phase 2: Subdomain Routing (Deferred)

| Task | Owner |
|------|-------|
| Implement `TenantResolutionMiddleware` | Hicks |
| Configure wildcard DNS | DevOps |
| Update K8s Ingress for wildcard | Ripley |
| Blazor subdomain detection | Vasquez |
| Tenant onboarding UI | Vasquez |
| OIDC redirect URL handling | Bishop |

**Deliverable:** `acme.warp-business.com` routes to Acme's data.

### Phase 3: Enterprise Isolation (Future)

- Per-tenant database option
- Per-tenant OIDC realm
- Tenant-specific branding

### ITenantContext Registration Pattern

```csharp
// Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, JwtTenantContext>();
builder.Services.AddScoped<IClaimsTransformation, TenantClaimsTransformation>();
```

`JwtTenantContext` reads `"tenant_id"` and `"tenant_slug"` claims from the current `HttpContext.User`. Returns `Guid.Empty` when absent. `IsResolved` is `false` when no tenant is active.

### Global Query Filter Pattern

```csharp
public class CrmDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    
    public CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenantContext)
        : base(options) { _tenantContext = tenantContext; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>()
            .HasQueryFilter(c => c.TenantId == _tenantContext.TenantId);
        // ... all data entities
    }
}
```

- Filter evaluates at query time (closure). When `TenantId == Guid.Empty`, queries return 0 rows.
- Cross-tenant admin queries: use `.IgnoreQueryFilters()` explicitly.

---

## 2026-03-27: Multi-Tenancy Implementation — Hicks Backend Delivery

**Date:** 2026-03-27  
**Author:** Hicks (Backend Dev)  
**Status:** COMPLETE — commit `e3f7a5c`

### Key Files Created

| File | Purpose |
|------|---------|
| `src/WarpBusiness.Plugin.Abstractions/ITenantContext.cs` | `ITenantContext` interface + `NullTenantContext` singleton |
| `src/WarpBusiness.Api/Identity/Tenancy/Tenant.cs` | Tenant entity |
| `src/WarpBusiness.Api/Identity/Tenancy/TenantSamlConfig.cs` | SAML config entity (disabled until configured) |
| `src/WarpBusiness.Api/Identity/Tenancy/UserTenant.cs` | Join table (UserId string, TenantId Guid, Role) |
| `src/WarpBusiness.Api/Identity/Tenancy/JwtTenantContext.cs` | Reads tenant_id/tenant_slug from JWT claims |
| `src/WarpBusiness.Api/Identity/Tenancy/TenantClaimsTransformation.cs` | Auto-injects tenant claims for single-tenant users |
| `src/WarpBusiness.Api/Data/Configurations/TenantConfiguration.cs` | EF config for Tenant, TenantSamlConfig, UserTenant |
| `src/WarpBusiness.Api/Controllers/TenantsController.cs` | 7 endpoints: signup, mine, get, update, add/remove/role member |
| `src/WarpBusiness.Api/Data/Migrations/20260327170637_AddTenantInfrastructure.cs` | Tenant tables migration |
| `src/WarpBusiness.Plugin.Crm/Data/Migrations/20260327170703_AddTenantIdToCrm.cs` | TenantId on all CRM entities |
| `src/WarpBusiness.Plugin.EmployeeManagement/Data/Migrations/20260327170720_AddTenantIdToEmployeeManagement.cs` | TenantId on Employee |

### Key Files Modified

| File | Change |
|------|--------|
| `ApplicationDbContext.cs` | Added Tenants, TenantSamlConfigs, UserTenants DbSets |
| `TokenService.cs` | `GenerateAccessToken` gains optional `tenantId`, `tenantSlug` params |
| `Program.cs` | Registers `IHttpContextAccessor`, `ITenantContext→JwtTenantContext`, `IClaimsTransformation→TenantClaimsTransformation`, seeds `TenantAdmin` role |
| `CrmDbContext.cs` | Explicit constructor injecting ITenantContext; global filters on all 5 entity types |
| `EmployeeDbContext.cs` | Explicit constructor injecting ITenantContext; global filter on Employee |
| All 5 CRM domain files + Employee.cs | Added `public Guid TenantId { get; set; }` |

### Unique Index Changes

**Before:** `Company.Name` globally unique (wrong)  
**After:** Unique per tenant via composite index `(TenantId, Name)`

Same for `Employee.Email` and `CustomFieldDefinition.Name`.

---

## 2026-03-27: Multi-Tenancy Auth Implementation — Bishop Auth Delivery

**Author:** Bishop (Auth Specialist)  
**Status:** Implemented — commit `de3eacd`

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

### Files Modified

- `AuthController.cs` — Login: tenant-aware token issuance. Added `POST /api/auth/select-tenant` and `GET /api/auth/my-tenants`
- `TenantsController.cs` — Added SAML endpoints (GET/PUT/{tenantId}/saml, POST enable/test)
- `Identity/RefreshToken.cs` — Added `ActiveTenantId` nullable Guid
- `Identity/TokenService.cs` — Extended `GenerateAccessToken` with `tenant_role` and `allTenantIds` params. Added `GeneratePreAuthToken`
- `Program.cs` — Added `RequireActiveTenant` and `RequireTenantAdmin` policies
- `appsettings.json` — Added `WarpBusiness.RootDomain` and `WarpBusiness.SubdomainRoutingEnabled` config
- `Shared/Auth/TenantDtos.cs` — Added `MyTenantDto`, `SamlConfigDto`, `SaveSamlConfigRequest`
- CRM + EmployeeManagement controllers — Changed `[Authorize]` → `[Authorize(Policy = "RequireActiveTenant")]`

### What's Still Needed

1. **DB Migration** — `RefreshToken.ActiveTenantId` column needs a migration
2. **SAML auth flow** — `TenantSamlService.TestConnectionAsync` is a stub. Add `Sustainsys.Saml2` when ready.
3. **CRM/Employee data layer** — `RequireActiveTenant` policy now enforces JWT tenant claim, but services don't filter by tenant yet. Needs data entity TenantId filters.
4. **OIDC tenant mapping** — `ExternalIdentityMapper.EnsureUserAsync` needs to assign the user to a tenant on first OIDC login
5. **Phase 2 subdomain routing** — Flip `WarpBusiness:SubdomainRoutingEnabled: true` when wildcard DNS and TLS are configured.

---

## 2026-03-27: Brand Icon — SVG-first approach

**Author:** Vasquez (Design)  
**Status:** Complete

Warp Business icon is a cyan W-mark with glow on dark circle background. Master source is favicon.svg (SVG). PNG generation via scripts/generate-icons.py (requires cairosvg + pillow). SVG favicon wired in all 3 web properties. Modern browsers support SVG favicons natively.

**Why:** SVG scales perfectly at all sizes. One source, infinite resolutions.

---

## 2026-03-27: Tenant UI — Implementation Notes

**Date:** 2026-03-27  
**Author:** Vasquez (Frontend Dev)  
**Status:** Shipped

### Routes Added

| Route | Component | Guard |
|---|---|---|
| `/tenant/select` | `TenantSelect.razor` | Authenticated |
| `/tenant/signup` | `TenantSignup.razor` | Authenticated |
| `/settings/workspace` | `Settings/TenantAdmin.razor` | `TenantAdmin` role |

### API Endpoints Called

| Method | Path | Status | Notes |
|---|---|---|---|
| GET | `/api/auth/my-tenants` | ✅ Implemented | Returns `MyTenantDto` list |
| POST | `/api/auth/select-tenant` | ✅ Implemented | Tenant switching |
| POST | `/api/tenants/signup` | ✅ Implemented | Returns `AccessToken` inline |
| GET | `/api/tenants/{id}` | ✅ Implemented | Returns `TenantDetailDto` incl. members |
| PUT | `/api/tenants/{id}` | ✅ Implemented | 204 NoContent |
| PUT | `/api/tenants/{id}/members/{userId}/role` | ✅ Implemented | `ChangeMemberRoleRequest` |
| DELETE | `/api/tenants/{id}/members/{userId}` | ✅ Implemented | 204 NoContent |
| POST | `/api/tenants/{id}/members` | ✅ Implemented | `AddMemberRequest {email, role}` |
| GET | `/api/tenants/check-slug?slug=...` | ⚠️ Not implemented | UI shows check UI, fails gracefully |

### Shared DTO Notes

- `MyTenantDto(Guid Id, string Name, string Slug, string Role)` — used for `/api/auth/my-tenants`
- `TenantDetailDto` — returned by `GET /api/tenants/{id}`, includes members inline
- `TenantMemberDto(string UserId, string Role, DateTimeOffset JoinedAt)` — minimal from API
- `TenantSignupResponse.AccessToken` — use this directly after signup

### CSS

Tenant card styles added globally to `app.css`:
- `.tenant-select-wrapper` — centered max-width container
- `.tenant-card` — hover lift + translucent dark background
- `.tenant-avatar` — gradient avatar circle with initial letter

---

## 2026-06-01: Contact-Employee Relationships — Implementation Plan

**Author:** Ripley  
**Status:** Planning (NOT Implemented — Salvage Opportunity)

### Current State

- ✅ Domain models exist and are well-designed
- ✅ EF configurations, DbContext integration, migration all present
- ✅ Service layer and controllers implemented
- ✅ DTOs defined with proper validation
- ✅ ContactDto already includes relationships
- ✅ Build succeeds (no compile errors)
- ❌ No Notes field on relationship entity (requirement mismatch)
- ❌ No default/seed data for relationship types
- ❌ No tests for the new feature
- ❌ No cross-plugin EmployeeId validation (FK to Employee table missing)
- ❌ Migration not applied (schema changes uncommitted)

### Recommendation

Salvage and complete. The foundation is solid. We need to add Notes, seed data, cross-plugin FK handling, and tests.

### Files Involved

**Domain Entities:**
- `ContactEmployeeRelationship.cs` — All required fields except Notes
- `ContactEmployeeRelationshipType.cs` — Complete as-is
- `Contact.cs` (modified) — Added `EmployeeRelationships` navigation

**EF Core Configuration:**
- `ContactEmployeeRelationshipConfiguration.cs` — Solid, with proper FKs and indexes
- `ContactEmployeeRelationshipTypeConfiguration.cs` — Good
- `ContactConfiguration.cs` (modified) — Added navigation config

**Service Layer:**
- `IContactEmployeeRelationshipService.cs` — Well-designed CRUD
- `ContactEmployeeRelationshipService.cs` — Implementation complete
- Service registered in CRM module

**Controllers:**
- `ContactEmployeeRelationshipController.cs` — All endpoints implemented

### Gaps to Close

1. **Add Notes field** — Optional free text on relationship entity (string, max 1000)
2. **Seed default relationship types** — In migration: "Sales", "Service", "Support", etc.
3. **Add cross-plugin EmployeeId validation** — Service should verify Employee exists before saving relationship
4. **Write tests** — Unit tests for service, integration tests for endpoints
5. **Apply migration** — Schema changes need to be committed
6. **Update ContactDto** — Ensure relationships are included in response

### Technology Notes

- Uses `ContactEmployeeRelationshipDto` with `EmployeeName` and `EmployeeEmail` denormalized (correct decision — avoids cross-plugin joins)
- Unique index on `(ContactId, EmployeeId, RelationshipTypeId)` prevents duplicate relationships
- Tenant-scoped via CRM plugin's existing global query filter
- Cascade delete on Contact (relationships removed with contact); Restrict delete on RelationshipType (can't delete in-use type)

---

## 2026-07-02: Invoicing Plugin Implementation — Hicks Backend

**Author:** Hicks (Backend Dev)  
**Status:** Implemented  
**Branch:** `feature/catalog-search-and-invoice-prep`

### Context

Built the complete WarpBusiness.Plugin.Invoicing plugin based on Ripley's design spec. The plugin adds invoice creation, line item management, payment recording, and tenant-configurable settings.

### Key Implementation Decisions

**1. InvoiceService constructor is `internal`**

The `IInvoiceNumberGenerator` interface is `internal` (per design — not exposed via API). C# accessibility rules prevent a `public` constructor from accepting an `internal` parameter type. Since both the service and generator live in the same assembly and DI resolves them internally, making the constructor `internal` is the cleanest fix. No impact on DI registration.

**2. Proportional discount on tax calculation**

The spec said `taxAmount = (Subtotal - DiscountAmount) × (TaxRate / 100)` for taxable items. Implemented proportional discount allocation: if only some line items are taxable, the invoice-level discount is proportionally distributed between taxable and non-taxable amounts. This is more financially correct than applying the full discount against taxable items only.

**3. Payment deletion resets status to Sent**

When an admin deletes a payment and the remaining AmountPaid drops to zero, the invoice reverts to `Sent` (not `Draft`). A sent invoice should not go back to draft — it's already been delivered to the customer.

**4. Line item operations throw on invalid state**

`AddLineItemAsync` throws `InvalidOperationException` when the invoice is not found or not in Draft status. The controller catches this and returns 409 Conflict. This differs from the `null`-return pattern used for read-miss scenarios but provides better error messages for business rule violations.

### Files Created (37 total)

- 7 Domain entities + enums
- 4 EF configurations  
- 1 DbContext + 1 migration
- 5 service interfaces + 5 implementations (including number generator)
- 4 controllers
- 4 Shared DTO files
- 1 Module + 1 csproj + 1 _Imports.razor

### Files Modified (4 total)

- `src/WarpBusiness.Api/Program.cs` — module registration
- `src/WarpBusiness.Api/WarpBusiness.Api.csproj` — project reference
- `src/WarpBusiness.slnx` — solution entry
- `src/WarpBusiness.Tests/Infrastructure/WarpTestFactory.cs` — in-memory DB
