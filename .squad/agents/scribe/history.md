# Project Context

- **Project:** warp-business-new
- **Created:** 2026-04-11

## Core Context

Agent Scribe initialized and ready for work.

## Recent Updates

📌 Team initialized on 2026-04-11

## Learnings

### Blazor Server Auth Token Forwarding (2026-04-11)

- In Blazor Server InteractiveServer mode, `IHttpContextAccessor.HttpContext` is null after the SignalR circuit establishes.
- `DelegatingHandler` instances from `IHttpClientFactory` run in a separate DI scope from the Blazor circuit — scoped services are different instances.
- Pattern: Use `CircuitHandler.OnCircuitOpenedAsync` to capture access token into a scoped `TokenProvider`. Typed HTTP clients (in circuit scope) set `DefaultRequestHeaders` from `TokenProvider` in their constructors.
- Key files: `TokenProvider.cs`, `TokenCircuitHandler.cs`, `UserApiClient.cs`, `TenantApiClient.cs`.
- This resolves 401 Unauthorized errors on interactive components requiring API authentication.
