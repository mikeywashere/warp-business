# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** Warp Business — Business Management System (CRM first)
- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core, Auth/Authz
- **Role:** Lead — architecture, code review, decisions
- **Created:** 2026-03-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2025-07-24: Aspire solution scaffolded

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
