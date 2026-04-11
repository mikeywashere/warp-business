# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, PostgreSQL
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-11: Initial .NET Aspire Architecture

**Solution Structure:**
- `WarpBusiness.sln` - Root solution file
- `WarpBusiness.AppHost` - Aspire orchestrator project, coordinates all resources and services
- `WarpBusiness.ServiceDefaults` - Shared Aspire configuration (health checks, telemetry, resilience)
- `WarpBusiness.Api` - ASP.NET Core Web API (middle tier)
- `WarpBusiness.Web` - Blazor Web App (frontend)

**Architecture Patterns:**
- Aspire orchestration pattern: AppHost references and manages all projects
- ServiceDefaults pattern: Shared health endpoints, telemetry, and service discovery configuration
- Project references flow: AppHost → {ServiceDefaults, Api, Web}; Api → ServiceDefaults; Web → ServiceDefaults
- Database integration: PostgreSQL added as Aspire resource with PgAdmin for management
- Service communication: Web references Api for service-to-service calls

**Key Files:**
- `WarpBusiness.AppHost\AppHost.cs` - Orchestration configuration (PostgreSQL, API, Web wiring)
- `WarpBusiness.Api\Program.cs` - API startup with ServiceDefaults integration
- `WarpBusiness.Web\Program.cs` - Blazor startup with ServiceDefaults integration

**Technical Decisions:**
- Using .NET 10 SDK (10.0.201) with Aspire 13.2.2 (NuGet packages, not workload)
- PostgreSQL as the database with PgAdmin included for development
- MapDefaultEndpoints() called in both API and Web for health checks/telemetry
- WithExternalHttpEndpoints() on Web project for external access
- WithPgAdmin() on PostgreSQL resource for database management UI
