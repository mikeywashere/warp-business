# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** Warp Business — Business Management System (CRM first)
- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core, Auth/Authz
- **Role:** Lead — architecture, code review, decisions
- **Created:** 2026-03-25

## Learnings

### 2025-07-24: Plugin/Module System Foundation

- **What was done:** Created `WarpBusiness.Plugin.Abstractions` and `WarpBusiness.Plugin.Sample`; both added to `WarpBusiness.slnx`; `docs/plugin-development.md` written.
- **Interface contract:** `ICustomModule` has five concerns: identity (`ModuleId`, `DisplayName`, `Version`, `Description`), DI registration (`ConfigureServices`), pipeline integration (`Configure`), nav contribution (`GetNavItems`), and Blazor page contribution (`GetBlazorAssemblies`).
- **Abstractions SDK:** `WarpBusiness.Plugin.Abstractions` uses `Microsoft.NET.Sdk` with a `FrameworkReference` to `Microsoft.AspNetCore.App`. This exposes `IServiceCollection`, `IConfiguration`, and `WebApplication` without pulling in a web SDK or NuGet package.
- **Sample SDK:** `WarpBusiness.Plugin.Sample` uses `Microsoft.NET.Sdk.Razor` (for `.razor` compilation) + same `FrameworkReference`. Added `_Imports.razor` to resolve `PageTitle` and other Blazor component usings.
- **`WithTags` omission:** Removed `.WithTags("Sample Plugin")` from the sample endpoint — it requires `Microsoft.AspNetCore.OpenApi` which is not a default dependency for plugin libraries.
- **Discovery mechanism (not yet implemented):** The host-side loader (scanning `plugins/` at startup, `AssemblyLoadContext` isolation, calling `ConfigureServices`/`Configure`) is the next step — tracked separately.
- **Decision:** `.squad/decisions/inbox/ripley-plugin-architecture.md`



- **What was done:** Full .NET Aspire solution created with 6 projects, all references wired, NuGet packages added, build + tests green.
- **.NET 10 uses `.slnx` format** — `dotnet new sln` creates `WarpBusiness.slnx`, not `.sln`. All tooling (`dotnet build`, `dotnet sln list`) works the same.
- **Aspire templates not pre-installed** — AppHost and ServiceDefaults were created manually. This is fine and gives us full control. The key pieces are:
  - `Aspire.AppHost.Sdk` (SDK import in AppHost csproj)
  - `IsAspireHost=true` property
  - `IsAspireSharedProject=true` for ServiceDefaults
  - `FrameworkReference Include="Microsoft.AspNetCore.App"` in ServiceDefaults
- **Solution file:** `src/WarpBusiness.slnx`
- **Key files:**
  - `src/WarpBusiness.AppHost/Program.cs` — wires PostgreSQL + PgAdmin, API with DB reference, Web with API reference
  - `src/WarpBusiness.ServiceDefaults/Extensions.cs` — OpenTelemetry, health checks, resilience, service discovery
  - `src/WarpBusiness.Api/Program.cs` — uses `AddServiceDefaults()` and `MapDefaultEndpoints()`
  - `src/WarpBusiness.Web/Program.cs` — same ServiceDefaults integration
- **ADR:** `docs/adr/001-aspire-solution-structure.md`
- **Decision:** `.squad/decisions/inbox/ripley-aspire-architecture.md`
- **Packages added:** `Aspire.Hosting.PostgreSQL` (AppHost), `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` (Api)

## 2026-03-26 — CRM Plugin Scaffold
- **Decision:** Extracted CRM domain to WarpBusiness.Plugin.Crm (Razor SDK, EF Core 10.0.5 / Npgsql 10.0.1).
- **Schema:** crm — isolated from identity schema, own CrmDbContext.
- **Entry point:** CrmModule : ICustomModule — registered same pattern as EmployeeManagement (first-party, explicit ConfigureServices/Configure in Program.cs).
- **Nav:** Contacts / Companies / Deals via GetNavItems().
- **Blazor:** Pages live in plugin assembly, contributed via GetBlazorAssemblies().
- **Rationale:** Keep host API lean (auth + plugin infra only). CRM is opt-in and independently deployable.

## 2026-03-26 — Local Kubernetes Deployment
- **What was done:** Created multi-stage .NET 10 Dockerfiles for Api, Web, CustomerPortal; `src/.dockerignore`; `docs/kubernetes-deployment.md`; root `.dockerignore`.
- **Build context:** All Dockerfiles build from `src/` — required for sibling project COPY (Plugin.Crm, Plugin.EmployeeManagement, Plugin.Sample, Shared, ServiceDefaults).
- **Aspire service discovery:** `services__api__http__0` env var pattern works in K8s manifests unchanged — no code modifications needed.
- **Ingress:** api/app/portal.warp-business.local → warp-api/warp-web/warp-portal:8080.
- **Plugin dir:** `/app/plugins` created in API image; mount a PVC to add external DLLs without rebuild.
- **Decision:** `.squad/decisions/inbox/ripley-k8s-strategy.md`

## 2026-03-27 — Multi-Tenancy Architecture Analysis

- **What was done:** Full multi-tenancy architecture analysis and recommendation for Warp Business.
- **Recommendation:** Shared schema with TenantId column (Option A) — simplest approach for current stage, clean upgrade path if needed.
- **Three models evaluated:**
  1. Shared schema + TenantId: Logical isolation via EF query filters. Low complexity, lowest cost. Risk: application bugs can leak data.
  2. Schema-per-tenant: PostgreSQL schema isolation. Medium complexity. Risk: schema explosion at scale.
  3. Database-per-tenant: Maximum isolation. High complexity, highest cost. Best for compliance-heavy enterprise.
- **TenantId propagation pattern:**
  - Phase 1: Resolve from JWT `tenant_id` claim
  - Phase 2: Subdomain extraction middleware (`acme.warp-business.com` → tenant slug)
  - `ITenantContext` scoped service injected into DbContexts
- **EF Core enforcement:** Global query filters on all data entities. Insert sets TenantId from context.
- **Schema changes identified:**
  - New `identity.Tenants` table (Id, Name, Slug, IsActive, Settings jsonb)
  - TenantId FK on: Company, Contact, Deal, Activity, CustomFieldDefinition, Employee
  - Unique indexes must become composite (TenantId + field) — e.g., `Company.Name` unique per tenant, not globally
- **Plugin isolation:** Each plugin DbContext (CrmDbContext, EmployeeDbContext) applies its own query filters. Shared `ITenantContext` from abstractions.
- **Migration strategy:** Add TenantId as nullable → create default tenant → backfill → make non-nullable → add FK
- **Auth intersection (for Bishop):** User-tenant membership model, JWT tenant claim, tenant switching, OIDC tenant mapping
- **Phased plan:**
  - Phase 1 (now): TenantId columns, query filters, JWT claim, test isolation
  - Phase 2 (later): Subdomain routing, wildcard DNS/Ingress, Blazor subdomain detection
  - Phase 3 (future): Per-tenant database option, per-tenant IdP, branding
- **Current state:** No tenancy infrastructure exists today — clean-slate implementation.
- **Decision:** `.squad/decisions/inbox/ripley-tenancy-architecture.md`
