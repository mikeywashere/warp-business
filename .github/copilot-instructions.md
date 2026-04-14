# Copilot Instructions for WarpBusiness

This document provides guidance for Copilot and other AI assistants working in the WarpBusiness codebase.

## Project Overview

**WarpBusiness** is a multi-project C#/.NET 10 business management platform with a Blazor Server frontend, ASP.NET Core REST API, and modular architecture. The solution uses:
- **Framework**: .NET 10.0 (net10.0 target)
- **Backend**: ASP.NET Core API with PostgreSQL via Entity Framework Core
- **Frontend**: Blazor Server with custom dark theme CSS
- **Authentication**: Keycloak (OpenID Connect on frontend, JWT Bearer on API)
- **Orchestration**: .NET Aspire for local development
- **Database**: PostgreSQL with multi-schema design (warp, employees schemas)

## Build and Run

### Build the Solution
```bash
dotnet build
```

### Run All Services (requires Docker/Podman for Postgres + Keycloak)
```bash
dotnet run --project WarpBusiness.AppHost
# Services start at:
# - API: http://localhost:5000
# - Web: http://localhost:5001
# - Marketing: http://localhost:5002
# - Keycloak: http://localhost:8080
# - PgAdmin: http://localhost:5050
```

### Run Individual Services
```bash
# API only (requires local PostgreSQL + Keycloak, or appsettings.Development.json override)
dotnet run --project WarpBusiness.Api

# Web only (requires API running)
dotnet run --project WarpBusiness.Web

# Marketing site (standalone)
dotnet run --project WarpBusiness.MarketingSite
```

### Database Migrations and Schema Creation

PostgreSQL setup uses two separate schemas and DbContexts:

```bash
# Warp system schema (Users, Tenants, Currencies, etc.)
# Uses MigrateAsync on startup
# Migrations in: WarpBusiness.Api/Data/Migrations/

# Employees schema (Employees, managed separately)
# Uses EnsureCreatedAsync in tests, requires explicit migration commands
# To create/update employee migrations:
cd WarpBusiness.Employees
dotnet ef migrations add <MigrationName> --context EmployeeDbContext
dotnet ef database update --context EmployeeDbContext
```

## Testing

### Run All Tests
```bash
dotnet test
```

### Run Tests by Project
```bash
dotnet test WarpBusiness.Api.Tests
dotnet test WarpBusiness.Web.Tests
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~EmployeeEndpointTests"
```

### Test Patterns
- **API tests**: Use `PostgreSqlFixture` with xUnit's `[Collection("Database")]` for shared fixture
- **Database reset**: `TestHelpers.EnsureEmployeeSchemaAsync()` + `db.Employees.RemoveRange()` 
- **HTTP context**: Tests inject `HttpContext` with `context.Items["TenantId"] = tenantId` for multi-tenancy
- **Assertions**: Use FluentAssertions; for nullable numeric "one of" checks, use `.Match(s => s == X || s == Y, reason)` not `.BeOneOf()`

## Project Structure

### WarpBusiness.Api
REST API with endpoints organized by resource:
- `Data/` — DbContexts (WarpBusinessDbContext), models, migrations
- `Endpoints/` — Endpoint implementations (minimal APIs style)
- `Models/` — Domain models (ApplicationUser, Tenant, Currency, etc.)
- `Services/` — Business logic (UserValidator, KeycloakAdminService, etc.)
- `Program.cs` — Dependency injection, authentication setup, middleware

**Key Dependencies**: EmployeeDbContext (from WarpBusiness.Employees), Keycloak Auth

### WarpBusiness.Web
Blazor Server frontend:
- `Components/` — Razor components (layouts, pages, shared UI)
- `Services/` — HTTP clients, token management (TokenProvider, AuthTokenHandler, UserApiClient)
- `wwwroot/app.css` — Dark theme CSS with custom properties
- `Program.cs` — OIDC configuration, HTTP client setup

**Token flow**: Proactive JWT exp check during SSR + reactive 401 catch-and-retry in AuthTokenHandler

### WarpBusiness.Employees
Modular feature library (separate DbContext pattern):
- `Data/EmployeeDbContext.cs` — Isolated schema "employees", own migrations
- `Models/Employee.cs` — Multi-tenant Employee entity
- `Endpoints/` — Employee CRUD endpoints
- `Services/` — Employee business logic

**Multi-tenancy**: EmployeeNumber + TenantId composite unique index

### WarpBusiness.AppHost
.NET Aspire orchestration:
- Postgres container + warpdb database
- Keycloak container + realm import (warpbusiness-realm.json)
- Custom Keycloak theme bind-mount (keycloak/themes/warp/)
- Service references and port mappings

### WarpBusiness.ServiceDefaults
Shared service configuration (logging, resilience, OpenTelemetry)

### WarpBusiness.MarketingSite
Public-facing marketing website (standalone)

## Key Architecture Decisions

### Multi-Schema Database Design
- **warp schema** (WarpBusinessDbContext): Users, Tenants, Currencies — uses migrations
- **employees schema** (EmployeeDbContext): Employees — separate DbContext, uses EnsureCreatedAsync in tests
- Both schemas share the same PostgreSQL connection (warpdb)
- New feature modules follow the same pattern: separate project + DbContext + schema

### Multi-Tenancy
- Tenant ID stored in HttpContext.Items["TenantId"] (boxed Guid, not Guid?)
- All tenant-scoped entities include TenantId in composite unique indices
- Middleware extracts tenant from JWT claim or header

### Employee-User Linking
- EmployeeDbContext and WarpBusinessDbContext are separate; cannot use EF navigation across contexts
- Linking logic in `WarpBusiness.Api/Endpoints/EmployeeUserEndpoints.cs` (needs both contexts)
- UserId index is filtered: `HasFilter("\"UserId\" IS NOT NULL")` to allow multiple null values

### Keycloak Integration
- API: JWT Bearer with Keycloak as token provider (RoleClaimType = "roles")
- Web: OpenID Connect (Authorization Code flow) with OIDC middleware
- Realm config: `warpbusiness-realm.json` (imported to Keycloak on first run)
- Custom theme: `keycloak/themes/warp/` (bind-mounted, not baked into image)
- Admin credentials: From Aspire KeycloakResource parameters

### Dark Theme Design System
- CSS custom properties (--clr-bg, --clr-accent, --clr-text, --clr-border, --clr-font-heading, --clr-font-body) in `:root`
- Bootstrap retained for layout/components but heavily overridden (~20+ component classes)
- Components styled: buttons, alerts, badges, forms, dropdowns, tables, list groups, etc.
- Avoid using --clr-accent on dark backgrounds without sufficient contrast; prefer --clr-text for text on dark

## Authorization and Authentication

### API (JWT Bearer)
```csharp
// Program.cs setup
options.RoleClaimType = "roles";  // Keycloak uses flat 'roles' claim

// Middleware chain (CRITICAL: role enrichment BEFORE UseAuthorization)
app.UseAuthentication();
app.UseMiddleware<RoleEnrichmentMiddleware>();  // Add roles to principal if needed
app.UseAuthorization();

// Endpoint authorization
[Authorize(Roles = "admin")]
public async Task HandleAsync(HttpContext ctx) { ... }
```

### Web (OIDC + Cookies)
```csharp
// Program.cs setup
AddCookie()               // Session cookies
AddOpenIdConnect()        // Keycloak OIDC
    .Authority = $"{keycloakUrl}/realms/warpbusiness"
    .ClientId = "warpbusiness-web"
    .SaveTokens = true    // Store access + refresh tokens
    .GetClaimsFromUserInfoEndpoint = true

// Token refresh: two-layer approach
// 1. Proactive: AuthenticatedComponentBase checks exp during SSR
// 2. Reactive: AuthTokenHandler catches 401 and refreshes
```

## Conventions and Best Practices

### Entity Framework Core
- Use filtered unique indices for optional unique columns:
  ```csharp
  entity.HasIndex(e => e.KeycloakSubjectId).IsUnique()
      .HasFilter("\"KeycloakSubjectId\" != ''");  // null-safe
  ```
- Composite unique indices must include TenantId for tenant-scoped uniqueness
- Property config: `.HasMaxLength()`, `.IsFixedLength()`, enum `.HasConversion<string>()`
- String enums: serialize as strings (not integers); use `.HasConversion<string>()`

### Keycloak API Integration
- `KeycloakAdminService.CreateUserAsync()` returns `KeycloakOperationResult` (not Task<string>)
- Handle 4xx responses (e.g., user exists) as 400 Bad Request, not 500/502
- Parse `ErrorMessage` from response for client feedback

### Testing Patterns
- Use `[Collection("Database")]` to share PostgreSqlFixture across tests in a class
- Reset state: remove entities + `SaveChangesAsync()`, not full migration rollback
- Create fresh DbContext per test: `TestHelpers.CreatePostgresEmployeeDbContext()`
- Never assume test ordering; each test must be independent

### CSS and Styling
- Define all theme colors in `:root` custom properties
- Group overrides by component (buttons, forms, tables, etc.)
- Use comments to mark Bootstrap override sections
- Test contrast in both light and dark themes (currently dark only)
- Bootstrap classes (.btn, .form-control, etc.) are OK for layout; override colors/borders

### Code Quality
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- Use `namespace` declarations (not fully qualified at top level)
- Minimal APIs style for endpoints (MapGet, MapPost, etc. in Program.cs or endpoint files)

## Common Tasks

### Add a New User Role
1. Create role in Keycloak admin console (or realm JSON)
2. Add role check in API endpoint: `[Authorize(Roles = "role-name")]`
3. Assign role to user in Keycloak
4. Test with JWT inspection (use jwt.io)

### Add a New Endpoint
1. Create endpoint file in `WarpBusiness.Api/Endpoints/` with naming pattern `{Resource}Endpoints.cs`
2. Implement as extension method on `WebApplication` or `RouteGroupBuilder`
3. Register in `Program.cs`: `app.MapXyzEndpoints()`
4. Write tests in `WarpBusiness.Api.Tests/Endpoints/` following xUnit + FluentAssertions pattern

### Add a New Feature Module (with Database)
1. Create new project `WarpBusiness.{ModuleName}` with structure:
   - `Data/{ModuleName}DbContext.cs` (separate DbContext, custom schema)
   - `Models/` (domain entities)
   - `Endpoints/` (API endpoints)
   - `Services/` (business logic)
2. Register DbContext in `WarpBusiness.Api/Program.cs`:
   ```csharp
   builder.AddNpgsqlDbContext<{ModuleName}DbContext>("warpdb");
   ```
3. Add project reference in `WarpBusiness.Api.csproj`
4. Create migrations (if needed): `dotnet ef migrations add ...`

### Update Keycloak Theme
1. Edit files in `keycloak/themes/warp/` (login, account, email templates)
2. Keycloak hot-reloads bind-mounted theme files during development
3. For production changes, rebuild the Keycloak image (theme is baked in)

### Refresh Database for Clean State (Development)
```bash
# Option 1: Delete Docker volumes (Aspire will recreate)
docker volume rm warpbusiness-postgres  # or similar

# Option 2: In-code reset (tests)
await TestHelpers.EnsureEmployeeSchemaAsync(db);
db.Employees.RemoveRange(db.Employees);
await db.SaveChangesAsync();
```

## Known Issues and Workarounds

### Table Dark Theme Contrast
The table header styling (`.table thead.table-dark th`) currently uses `--clr-accent` for text color, which can have low contrast on dark backgrounds. When updating table styling, use `--clr-text` for headers and `--clr-text-muted` for secondary text to ensure readability.

### Keycloak Data Volume Persistence
Changes to `warpbusiness-realm.json` only apply on first container run. To apply updates:
1. Delete the Keycloak data volume: `docker volume rm <volume-name>`
2. Restart Aspire: `dotnet run --project WarpBusiness.AppHost`

### EF Core Migrations with Multiple DbContexts
Always specify `--context` when running `dotnet ef` commands for the Employees project:
```bash
dotnet ef migrations add MyMigration --context EmployeeDbContext
```

## Team Conventions

### File Organization
- Group related classes by feature/resource (Employees, Users, etc.)
- Use nested folders only when a feature grows significantly
- Keep endpoint files with their feature (e.g., `Endpoints/EmployeeEndpoints.cs`)

### Naming Conventions
- C# classes: PascalCase (ApplicationUser, EmployeeDbContext)
- Database tables: PascalCase in SQL (mapped from class names)
- API endpoints: RESTful (GET /users/{id}, POST /users, etc.)
- CSS custom properties: kebab-case (--clr-accent, --font-heading)

### Comments and Documentation
- Only comment non-obvious business logic; avoid commenting obvious code
- Use `//` for implementation notes, `///` for public API docs (XML docs)
- Keycloak-specific quirks and JWT claim details should be documented

## MCP Server Configuration (Optional)

If you plan to extend Copilot's capabilities with custom tools or integrations, see `.github/copilot-extensions/` for MCP server configuration (if present). Current extensions are managed via the Squad agent configuration in `.github/agents/squad.agent.md`.

## Related Resources

- **Solution File**: `WarpBusiness.slnx` (defines all projects)
- **Architecture Diagram** (if present): Check `/docs/`
- **API Documentation**: OpenAPI/Swagger available at `/swagger` when API is running
- **Keycloak Docs**: https://www.keycloak.org/documentation
- **.NET Aspire Docs**: https://learn.microsoft.com/en-us/dotnet/aspire/

---

**Last Updated**: 2025  
**Framework**: .NET 10.0  
**Maintainers**: See CODEOWNERS (if present)
