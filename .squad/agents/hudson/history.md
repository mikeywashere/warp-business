# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** Warp Business — Business Management System (CRM first)
- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core, Auth/Authz
- **Role:** Tester — unit tests, integration tests, quality assurance, edge cases
- **Created:** 2026-03-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

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

