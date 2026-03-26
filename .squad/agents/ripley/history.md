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
