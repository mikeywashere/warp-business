# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** Warp Business — Business Management System (CRM first)
- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core, Auth/Authz
- **Role:** Tester — unit tests, integration tests, quality assurance, edge cases
- **Created:** 2026-03-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-26: CRM Plugin Extraction — Test Factory Pattern

**Plugin DbContexts must be swapped for in-memory in WarpTestFactory:**
`CrmModule.ConfigureServices` and `EmployeeManagementModule.ConfigureServices` both require a `warpbusiness` connection string and register Npgsql-backed DbContexts. In tests, provide a dummy connection string via `builder.UseSetting("ConnectionStrings:warpbusiness", "...")` BEFORE `ConfigureServices` runs, then remove and replace each plugin DbContext with `UseInMemoryDatabase` in `ConfigureServices`.

**AddApplicationPart is already in Program.cs:**
Both plugin assemblies are already registered with `AddApplicationPart` in `Program.cs`. `WebApplicationFactory<Program>` inherits this — no need to repeat it in `WarpTestFactory`. The main fix is replacing the DbContexts and adding plugin project references to Tests.csproj.

**Tests.csproj must reference plugin projects directly:**
`WarpBusiness.Tests` must have `<ProjectReference>` entries for `WarpBusiness.Plugin.Crm` and `WarpBusiness.Plugin.EmployeeManagement` so `WarpTestFactory` can reference their DbContext types at compile time.

**Reusable helper pattern for swapping DbContexts:**
Extract a `ReplaceWithInMemory<TContext>(IServiceCollection, string)` helper in `WarpTestFactory` to avoid repeating the descriptor-removal logic for each DbContext. Pass a unique `Guid.NewGuid()` per DbContext per factory instance to isolate in-memory stores.

### 2026-03-26: Custom Fields Integration Tests

**Duplicate-name uniqueness is enforced in the controller, not the service:**  
`CustomFieldService.CreateDefinitionAsync` has no uniqueness guard. The controller does an `AnyAsync` check before calling the service and returns `409 Conflict` if a field with the same `Name + EntityType` already exists. In-memory EF does not enforce DB unique indexes, so this application-level check is mandatory in tests.

**Admin pattern for custom field tests:**  
Follow the same promote-then-relogin flow as `AdminControllerTests`: register → `PromoteToAdminAsync` → POST `/api/auth/login` → `SetBearerToken(auth.Token)`. The fresh token carries the Admin role claim required by `[Authorize(Roles = "Admin")]`.

**GetContact always returns all active field definitions:**  
`CustomFieldService.GetValuesForContactAsync` returns every active `Contact` definition with `Value = null` for fields the contact hasn't set. Tests asserting "all definitions included" should expect null values for unset fields, not missing entries.

### 2026-03-25: Integration Test Infrastructure

**WebApplicationFactory setup pattern:**  
Use `WarpTestFactory : WebApplicationFactory<Program>` with `Program` exposed via `public partial class Program { }` at the end of `Program.cs`. `ConfigureWebHost` overrides services after normal startup wires.

**In-memory DB replacement — remove ALL context descriptors:**  
Simply removing `DbContextOptions<ApplicationDbContext>` is not enough. Must also remove `IDbContextOptionsConfiguration<T>` (the lambda that calls `UseNpgsql`). Use: `services.Where(d => d.ServiceType == typeof(DbContextOptions<T>) || d.ServiceType == typeof(T) || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Any(t => t == typeof(T))))`.

**DB name capture — critical gotcha:**  
`services.AddDbContext<T>(o => o.UseInMemoryDatabase("name-" + Guid.NewGuid()))` evaluates `Guid.NewGuid()` on EVERY DbContext resolution. Capture the name first: `var dbName = "..."; services.AddDbContext<T>(o => o.UseInMemoryDatabase(dbName))`. Without this, roles seeded at startup are not visible to request handlers.

**Shared HttpClient auth state:**  
`IClassFixture<WarpTestFactory>` shares one client per test class. Tests that assert "no auth" must use `_factory.CreateClient()` to get a clean client, not the shared `_client` that may have bearer headers from a previous test.

**Aspire service defaults in test host:**  
`builder.AddServiceDefaults()` registers OpenTelemetry, health checks, and service discovery — none of which fail in WebApplicationFactory. They can be left in place; no special test override needed.

### 2026-03-25: Cookie-based auth in integration tests

**Secure cookies require HTTPS in tests:**  
Cookies with `Secure = true` are only sent over HTTPS. Test clients created with `factory.CreateClient()` default to HTTP. For refresh token tests (or any cookie-based auth), create the client with HTTPS: `factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") })`.

**Environment-specific migrations:**  
`Program.cs` calls `db.Database.MigrateAsync()` in Development, which fails with in-memory databases ("relational-specific methods"). Set test environment to "Test" via `builder.UseEnvironment("Test")` in `WarpTestFactory` to skip migrations. In-memory DB doesn't need them anyway.

**Token claims vs. DB roles:**  
JWT tokens capture roles at generation time. If you promote a user to Admin AFTER registering (via `UserManager.AddToRoleAsync`), the original token won't have the Admin claim. Tests must login AGAIN after role changes to get a fresh token with updated claims.

### 2026-03-26: Missing Controller Integration Tests

**CompaniesController has no role-based delete restriction:**  
`DeleteCompany` is only gated by `[Authorize]` (no role). Tests for "Admin only delete" would give false confidence — any authenticated user can delete. Adjusted test names to reflect reality: `DeleteCompany_ReturnsNoContent_WhenAuthenticated` and `DeleteCompany_ReturnsUnauthorized_WhenNotAuthenticated`.

**EmployeesController returns anonymous objects, not typed DTOs:**  
`EmployeesController.ToDto()` returns an anonymous `object`. Tests must use `System.Text.Json.JsonElement` to inspect response properties rather than a typed DTO class. Use `GetProperty("firstName").GetString()` and `GetProperty("id").GetGuid()` patterns.

**Per-test isolated clients prevent auth state bleed:**  
Each test that needs auth should call `_factory.CreateClient()` and authenticate fresh rather than sharing a `_client` with `SetBearerToken`. This prevents role-bearing tokens (Admin) from contaminating tests that assert on regular users.

**ActivitiesController accepts ContactId in CreateActivityRequest:**  
The `contactId` filter on `GET /api/activities?contactId={id}` only works if an activity was created with that `ContactId`. Tests must first create a Contact (via `POST /api/contacts`), then create an Activity referencing it before filtering.

