# ADR-001: .NET Aspire Solution Structure

**Status:** Accepted
**Date:** 2025-07-24
**Decision Maker:** Ripley (Lead)

## Context

Warp Business CRM needs a multi-project solution with a Blazor frontend, ASP.NET Core Web API backend, and PostgreSQL persistence. We need service orchestration for the dev inner loop, consistent telemetry/health/resilience across services, and a clear separation of concerns.

## Decision

Use .NET Aspire as the orchestration layer for the Warp Business CRM solution.

## Solution Layout

| Project | Type | Responsibility |
|---|---|---|
| `WarpBusiness.AppHost` | Aspire Host | Entry point. Orchestrates all services, provisions PostgreSQL, configures service discovery. |
| `WarpBusiness.ServiceDefaults` | Class Library | Shared configuration: OpenTelemetry, health checks, resilience, service discovery. Referenced by all service projects. |
| `WarpBusiness.Api` | ASP.NET Core Web API | ALL business logic lives here. Exposes REST endpoints. Owns the database via EF Core. |
| `WarpBusiness.Web` | Blazor Web App | Frontend. Server-rendered with interactive server components. Talks to API only — no direct DB access. |
| `WarpBusiness.Shared` | Class Library | DTOs, request/response contracts, shared enums. Referenced by Api, Web, and Tests. |
| `WarpBusiness.Tests` | xUnit Test Project | Unit and integration tests. References Api and Shared. |

## Project Reference Graph

```
AppHost → Api, Web (orchestration)
Api → ServiceDefaults, Shared
Web → ServiceDefaults, Shared
Tests → Api, Shared
```

## Blazor → API Communication

- The Blazor Web App communicates with the API exclusively via HTTP using typed `HttpClient` instances.
- Aspire service discovery handles endpoint resolution — no hardcoded URLs.
- The AppHost wires `web → api` via `WithReference(api)`, so the Web project can resolve the API by its resource name `"api"`.
- Typed HttpClient services are registered in the Web project's DI container.

## PostgreSQL

- The AppHost provisions a PostgreSQL container via `builder.AddPostgres("postgres")` with PgAdmin for dev tooling.
- A database named `"warpbusiness"` is added to the server.
- The API project consumes the database via `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, which auto-configures the connection string from Aspire.
- EF Core is used code-first — migrations are owned by the Api project.

## Authentication & Authorization

**Recommendation:** ASP.NET Core Identity hosted in the API project.

- The API project owns identity: user registration, login, token issuance.
- JWT bearer tokens for API-to-API and programmatic access.
- Cookie authentication for the Blazor Web App session (cookie issued by API, forwarded by Blazor).
- The Blazor frontend authenticates via the API — it never touches the identity store directly.
- Bishop (team member) owns the auth implementation.

## Key Architectural Patterns

1. **Vertical Slice Architecture** in the API — features are organized by business capability, not by technical layer.
2. **Thin Controllers / Minimal APIs** — endpoints are routing + validation only; logic lives in handlers/services.
3. **EF Core Code-First** — the database schema is defined by C# entity classes and managed via migrations.
4. **No business logic in the frontend** — Blazor is a thin UI layer that calls the API for all operations.

## Consequences

- Developers must run the AppHost to get the full stack (Aspire handles this cleanly).
- All inter-service communication goes through Aspire service discovery — no magic strings.
- The Shared project must remain dependency-free (no EF Core, no ASP.NET references).
- Adding new services later (e.g., background workers) follows the same pattern: create project, add to AppHost, reference ServiceDefaults.
