# Warp Business

A modular, plugin-first business management system built on .NET 10.

## Overview

Warp Business is a platform for managing customer relationships, employees, and business operations through a flexible plugin architecture. Start with the bundled CRM—contacts, companies, deals, and activities—then extend it with plugins by dropping DLLs into a folder. The system auto-discovers and loads them with no code changes to the core.

Built on modern .NET with Blazor frontend and ASP.NET Core backend, it includes separate portals for internal staff and external customers, identity federation through OIDC (Microsoft, Keycloak, or local), and a structured plugin system (`ICustomModule` interface) that lets you add features without touching core code.

The architecture prioritizes modularity: every major feature is either a plugin or a service consumed by plugins. Authentication is provider-agnostic. The frontend dynamically adapts to loaded plugins. The database schema evolves via EF Core migrations that plugins can contribute.

## Features

### Core
- **CRM** — Contacts, companies, deals (pipeline), and activities. Fully linked domain model with ownership, audit trails, and relationship navigation.
- **Custom Fields** — Add custom fields per contact at runtime (admin-defined, type-aware: text, number, date, boolean, select). Store and retrieve without schema migrations.
- **Employee Management** — Track employees, store contact info, soft and hard delete options.
- **Authentication** — ASP.NET Core Identity + JWT locally, or OIDC with Microsoft / Keycloak. Admin UI for user and auth provider management. Automatic token refresh with HttpOnly cookies.
- **Customer Portal** — Separate Blazor container. Customers log in (local or OIDC) and view/edit their own contact profile, with role-based access control.

### Plugin System
- **Auto-Discovery** — Drop a DLL in the `plugins/` folder. System loads it on startup via isolated assembly contexts.
- **ICustomModule Interface** — Plugins implement one contract: register controllers, Blazor components, EF migrations, and nav items.
- **Dynamic Navigation** — Main app nav updates at runtime based on plugin-registered menu items.
- **Service Integration** — Plugins access shared services (auth, database, logging) through dependency injection.

## Architecture

```
┌─────────────────────────────────────────┐
│      WarpBusiness.AppHost               │
│  .NET Aspire Orchestration               │
└────────────┬────────────────────────────┘
             │
   ┌─────────┼──────────┬─────────────────┐
   │         │          │                 │
   ▼         ▼          ▼                 ▼
┌──────┐ ┌─────────┐ ┌──────────┐ ┌──────────────┐
│ API  │ │  Web    │ │Customer  │ │  Database    │
│      │ │ (Blazor)│ │ Portal   │ │  (PostgreSQL)│
└──────┘ └─────────┘ └──────────┘ └──────────────┘
   │         │          │
   │ Plugin  │ Blazor   │ OIDC Auth
   │ Loading │ Server   │ (Microsoft/Keycloak)
```

**Components:**

- **WarpBusiness.Api** — ASP.NET Core Web API. Hosts plugins, loads DLLs from `plugins/` folder, exposes REST endpoints, handles auth/authorization.
- **WarpBusiness.Web** — Blazor Server frontend. Typed HTTP client discovers API via Aspire. Nav menu driven by loaded plugins. Edit forms, paged tables, modal confirmations.
- **WarpBusiness.CustomerPortal** — Separate Blazor app for external customers. Own database context, own auth flow.
- **WarpBusiness.Plugin.Abstractions** — `ICustomModule` interface, base exception types. Consumed by all plugins.
- **WarpBusiness.Plugin.Crm** — First-party CRM plugin. Contact, Company, Deal, Activity services and controllers.
- **WarpBusiness.Plugin.EmployeeManagement** — First-party employee plugin. Service and API.
- **WarpBusiness.Plugin.Sample** — Minimal plugin example for reference.
- **WarpBusiness.Shared** — DTOs, contracts, and utilities shared across frontend and backend.
- **WarpBusiness.ServiceDefaults** — Shared Aspire configuration for telemetry, health checks, service discovery.
- **WarpBusiness.Tests** — xUnit integration tests with in-memory EF Core database and WebApplicationFactory.

**Database:** PostgreSQL 16+, EF Core Code-First. Migrations live in each plugin and core API; all applied on startup.

## Getting Started

### Prerequisites

- **.NET 10 SDK** (or later)
- **Docker Desktop** or **Podman** (for local Postgres or K8s)
- **PostgreSQL 16** (if running standalone) or **Kubernetes** cluster (Docker Desktop, minikube, kind)

### Local Development with Aspire

Aspire orchestrates all services with automatic service discovery:

```bash
cd src
dotnet run --project WarpBusiness.AppHost
```

Aspire starts the API, Web, Customer Portal, and PostgreSQL. The dashboard (usually `http://localhost:15000`) shows all service status and logs.

### Kubernetes (Local Cluster)

Supports Docker Desktop Kubernetes, minikube, and kind.

**First-time setup:**

```bash
# Copy and customize secrets
cp k8s/secrets.yaml.template k8s/secrets.yaml
# Edit k8s/secrets.yaml: database URL, JWT key, OIDC client IDs, etc.

# Build container images and deploy
make build
make deploy      # applies kustomization from k8s/

# Verify
make status
```

For detailed K8s configuration, see `docs/kubernetes-deployment.md`.

### Running Tests

```bash
dotnet test src/WarpBusiness.sln
```

xUnit tests run against in-memory EF Core database. Full ASP.NET Core pipeline runs (auth, validation, routing). See `.squad/decisions.md` for integration test strategy.

### Configuration

**Authentication provider** is set in `appsettings.json`:

```json
{
  "AuthProvider": {
    "ActiveProvider": "Local"  // "Local", "Keycloak", or "Microsoft"
  }
}
```

See `docs/auth-providers.md` for provider-specific configuration.

**Database:** Connection string in `appsettings.json` or environment variable `ConnectionStrings:Default`.

## Plugin Development

### Quick Start

1. Create a class library targeting `.NET 10`.
2. Reference `WarpBusiness.Plugin.Abstractions`.
3. Implement `ICustomModule`:

```csharp
public class MyPlugin : ICustomModule
{
    public string Name => "My Plugin";
    
    public void Register(IServiceCollection services, IWebHostEnvironment env)
    {
        // Register services, add EF migrations, etc.
    }
    
    public void Configure(WebApplication app)
    {
        // Register controllers, Blazor components, nav items.
    }
}
```

4. Build to a DLL and place in `plugins/` folder alongside the API executable.
5. API auto-discovers and loads on startup.

### What Plugins Can Do

- **Register Controllers** — Use `app.Services.AddApplicationPart()` to register controller types from your assembly.
- **Add Blazor Components** — Register routes in `app.MapRazorComponents()`.
- **Contribute Migrations** — Add EF migrations to the shared DbContext; they run on startup.
- **Register Navigation Items** — Call a nav service to add menu items to the main app.
- **Access Core Services** — Dependency injection gives you Auth, Database, Logging, etc.

See `docs/plugin-development.md` and `WarpBusiness.Plugin.Sample` for examples.

## Project Status

**Actively under development.** Core CRM, employee management, and authentication are functional. Customer portal complete. Plugin system tested with first-party plugins. K8s deployment ready for local clusters.

Known issues and in-progress work tracked in GitHub Issues. See `.squad/decisions.md` for architectural decisions and team roles.

## Documentation

- `docs/auth-providers.md` — OIDC provider setup (Microsoft, Keycloak, Local)
- `docs/kubernetes-deployment.md` — K8s deployment guide
- `docs/plugin-development.md` — Detailed plugin development guide
- `.squad/decisions.md` — Architectural decisions, status, and implementation notes

## License

See LICENSE file.
