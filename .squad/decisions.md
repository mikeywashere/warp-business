# Squad Decisions

## Active Decisions

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
