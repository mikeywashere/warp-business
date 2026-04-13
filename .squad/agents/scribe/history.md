# Project Context

- **Project:** warp-business-new
- **Created:** 2026-04-11

## Core Context

Agent Scribe initialized and ready for work.

## Recent Updates

📌 Team initialized on 2026-04-11

### Orchestration Work (2026-04-13)

- **Orchestration Log:** Documented Geordi's branding rebrand (2026-04-13T0826-geordi.md) — favicon, fonts, CSS tokens, navbar, home page, dark theme, build passing
- **Session Log:** Created brief session summary (2026-04-13T0826-branding.md)
- **Decision Inbox Merge:** Consolidated 10 inbox decisions into main decisions.md; 6 decisions from Data, 2 from Geordi, 2 from Worf; deduplicated and organized by topic
- **Cross-Agent History:** Updated Geordi and Scribe agent histories to reflect completed work
- **Git Staging:** Staged .squad/ directory for commit

## Learnings

### Blazor Server Auth Token Forwarding (2026-04-11)

- In Blazor Server InteractiveServer mode, `IHttpContextAccessor.HttpContext` is null after the SignalR circuit establishes.
- `DelegatingHandler` instances from `IHttpClientFactory` run in a separate DI scope from the Blazor circuit — scoped services are different instances.
- Pattern: Use `CircuitHandler.OnCircuitOpenedAsync` to capture access token into a scoped `TokenProvider`. Typed HTTP clients (in circuit scope) set `DefaultRequestHeaders` from `TokenProvider` in their constructors.
- Key files: `TokenProvider.cs`, `TokenCircuitHandler.cs`, `UserApiClient.cs`, `TenantApiClient.cs`.
- This resolves 401 Unauthorized errors on interactive components requiring API authentication.
