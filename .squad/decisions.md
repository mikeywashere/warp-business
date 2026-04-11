# Squad Decisions

## Active Decisions

### Decision: Keycloak Authentication Architecture

**Date:** 2026-04-11
**Author:** Data (Backend Dev)
**Status:** Active

#### Context

The project needed authentication and authorization. Keycloak was chosen as the identity provider, integrated via .NET Aspire's official hosting and authentication packages.

#### Decision

##### Identity Provider: Keycloak via Aspire

- Keycloak runs as an Aspire container resource on port 8080 (pinned for OIDC cookie stability).
- Data volume ensures state persists across `dotnet run` restarts.
- Realm import from `WarpBusiness.AppHost/KeycloakConfiguration/` auto-provisions the `warpbusiness` realm on first start.

##### Two Clients

1. **warpbusiness-web** — Public OIDC client for the Blazor frontend. Standard flow + direct access grants enabled. Redirect URIs set to `*` for development.
2. **warpbusiness-api** — Bearer-only client for the API. No interactive login.

##### API Authentication

- Uses `Aspire.Keycloak.Authentication` package with `AddKeycloakJwtBearer()`.
- Connection string to Keycloak is resolved via Aspire service discovery (the `"keycloak"` resource name).
- Weatherforecast endpoint requires authorization as proof of integration.

##### Package Versions

- Both Keycloak packages are at `13.2.2-preview.1.26207.2` (preview — no stable 13.2.2 release yet for Keycloak components).

#### Consequences

- ✅ Zero manual Keycloak setup — realm, clients, and test user are provisioned automatically.
- ✅ Aspire service discovery handles Keycloak URL resolution for both API and Web.
- ✅ Bearer-only API client means the API never handles login flows.
- ⚠️ Preview packages — monitor for stable release and update when available.
- ⚠️ Wildcard redirect URIs must be locked down before production.

### Decision: OIDC Authentication in Blazor Web App

**Date:** 2026-04-11
**Author:** Geordi (Frontend Dev)
**Status:** Active

#### Context

The Web project needed OIDC authentication against a Keycloak identity provider being set up by Data in the AppHost.

#### Decision

Used standard `Microsoft.AspNetCore.Authentication.OpenIdConnect` (v10.0.5) rather than a third-party Aspire Keycloak client package, since no official one exists.

##### Key Choices

- **Cookie + OIDC dual scheme**: Cookie as default scheme, OpenIdConnect as challenge scheme
- **Keycloak URL from Aspire config**: Reads `services:keycloak:https:0` / `services:keycloak:http:0` — compatible with Aspire service discovery
- **Minimal API login/logout**: `/login` triggers OIDC challenge, `/logout` signs out of both cookie and OIDC
- **CascadingAuthenticationState via DI**: Uses `AddCascadingAuthenticationState()` instead of wrapping in `<CascadingAuthenticationState>` component
- **NameClaimType set to `preferred_username`**: Keycloak's standard claim for user display name

#### Rationale

- Standard OIDC package is well-supported, framework-aligned, and avoids third-party dependency risk
- Minimal API endpoints for login/logout are simpler than Razor pages and don't need antiforgery
- DI-based cascading auth state is the modern .NET 8+ Blazor pattern

#### Consequences

- ✅ Clean integration with Aspire service discovery
- ✅ No extra third-party dependencies
- ⚠️ Keycloak realm `warpbusiness` and client `warpbusiness-web` must be configured to match (Data's responsibility in AppHost)
- ⚠️ `RequireHttpsMetadata` is disabled in Development — acceptable for local Keycloak

### Decision: .NET Aspire Project Structure

**Date:** 2026-04-11  
**Author:** Riker (Lead)  
**Status:** Active

#### Context

Michael requested a .NET Aspire application with a Blazor web frontend, an ASP.NET Core Web API middle tier, and a PostgreSQL database. The architecture needed to support modern cloud-native development with built-in observability, service discovery, and orchestration.

#### Decision

We have created a multi-project .NET Aspire solution with the following structure:

##### Projects

1. **WarpBusiness.AppHost** - The Aspire orchestrator
   - Manages all services and resources
   - Configures PostgreSQL with PgAdmin
   - References and wires up API and Web projects
   - Entry point for running the entire application stack

2. **WarpBusiness.ServiceDefaults** - Shared configuration library
   - Provides common Aspire defaults (health checks, telemetry, resilience)
   - Referenced by all service projects (API and Web)
   - Centralizes cross-cutting concerns

3. **WarpBusiness.Api** - Middle tier API
   - ASP.NET Core Web API with minimal APIs
   - Weather forecast endpoint as starter template
   - References ServiceDefaults for Aspire integration
   - Will connect to PostgreSQL database

4. **WarpBusiness.Web** - Frontend application
   - Blazor Web App with interactive server components
   - References ServiceDefaults for Aspire integration
   - Configured to call the API for data

##### Database Strategy

- **PostgreSQL** chosen for relational data storage
- Provisioned via Aspire's `AddPostgres()` with PgAdmin included
- Database named "warpdb"
- API project will reference the database resource

##### Service Communication

- Web → API: Service-to-service communication via Aspire service discovery
- API → Database: Connection string managed by Aspire orchestration
- All services expose health endpoints via `MapDefaultEndpoints()`

#### Rationale

**Why Aspire?**
- Built-in orchestration eliminates docker-compose complexity
- Service discovery and configuration management out of the box
- Integrated observability (logging, metrics, tracing) from day one
- Local development experience matches cloud deployment

**Why This Structure?**
- **AppHost separation**: Keeps orchestration concerns isolated from business logic
- **ServiceDefaults**: DRY principle for Aspire configuration across services
- **Clear layers**: Frontend (Web) → API (Business Logic) → Database (Persistence)
- **Minimal coupling**: Each project has clear boundaries and responsibilities

**Why PostgreSQL?**
- Robust relational database with excellent .NET support
- PgAdmin provides GUI for development and debugging
- Aspire has first-class PostgreSQL integration

#### Consequences

##### Positive
- ✅ Rapid local development with full-stack orchestration
- ✅ Built-in telemetry and health checks from the start
- ✅ Clear separation of concerns between projects
- ✅ Easy to add additional services (Redis, message queues, etc.)
- ✅ Development experience is consistent with production

##### Considerations
- ⚠️ Aspire is relatively new; stay current with updates
- ⚠️ Team needs to understand Aspire orchestration model
- ⚠️ ServiceDefaults warning is expected (it's a library, not executable)

#### Alternatives Considered

1. **Docker Compose** - More manual configuration, less integrated tooling
2. **Separate Blazor + API solutions** - More complex to orchestrate locally
3. **SQL Server** - Chose PostgreSQL for cross-platform consistency

#### Implementation Notes

- .NET 10 SDK (10.0.201) with Aspire 13.2.2
- Aspire templates installed via `Aspire.ProjectTemplates` NuGet package
- Build succeeds with expected ASPIRE004 warning about ServiceDefaults
- Ready for feature development and database migrations

#### Next Steps

1. Add Entity Framework Core to the API project
2. Configure database migrations for PostgreSQL
3. Implement actual API endpoints beyond weather forecast
4. Wire up Blazor frontend to consume API
5. Add authentication and authorization

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
