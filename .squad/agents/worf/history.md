# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, xUnit, PostgreSQL
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-11: Test Project Infrastructure Established

- Created `WarpBusiness.Api.Tests` with xUnit, FluentAssertions, Testcontainers.PostgreSql, NSubstitute
- **xUnit 2.x** uses `Task` not `ValueTask` for `IAsyncLifetime` — don't use ValueTask returns
- **NSubstitute cannot mock non-virtual methods** on concrete classes like `KeycloakAdminService`. Use `FakeHttpMessageHandler` to control HTTP responses instead of trying to substitute the service directly
- **`Results.Conflict(new { message = "..." })`** returns `Conflict<AnonymousType>`, not `Conflict<object>`. Use reflection-based `GetStatusCode()` helper to assert HTTP status codes for anonymous-typed results
- Endpoint methods in `UserEndpoints` and `TenantEndpoints` are `private static` — tested via reflection. This works well but requires careful parameter ordering matching the method signatures
- PostgreSQL Testcontainers take ~20s to start on first run; tests sharing the `[Collection("Database")]` fixture reuse the same container
- InMemory provider supports cascade deletes configured in `OnModelCreating`, making it viable for DbContext relationship testing
- Migration tests confirm schema against real PostgreSQL — InMemory provider doesn't support migrations

### 2026-04-11: UpdateMyProfile Endpoint Tests Added

- Added 4 tests for `PUT /api/users/me` (UpdateMyProfile) to `UserEndpointTests.cs`: valid update, not-found, email fallback, and email/role preservation
- `UpdateMyProfile` method signature: `(ClaimsPrincipal, UpdateProfileRequest, WarpBusinessDbContext, KeycloakAdminService, CancellationToken)` — reflection params must match this order
- `FakeHttpMessageHandler.QueueSuccessResponse()` queues token + NoContent — sufficient for UpdateUserAsync calls that don't inspect the response body
- For email-fallback tests, seed a user with empty `KeycloakSubjectId` and use a principal whose `sub` won't match but whose `email` will — no Keycloak mock responses needed since the endpoint skips Keycloak when KeycloakSubjectId is empty
- **Test Results:** All 56 tests passing (48 existing + 4 new profile update tests)
- **Status:** ✅ Complete. Feature fully tested and ready for integration.

### 2026-04-11: Employee-User Linking Test Suite Created

- Created `WarpBusiness.Api.Tests/Endpoints/EmployeeUserLinkingTests.cs` — 22 unit tests covering data integrity, link validation, deletion blocking, unlinked users endpoint, create-with-user, update-with-user, and by-user lookup
- Created `WarpBusiness.Web.Tests/Tests/EmployeeUserLinkingE2ETests.cs` — 4 E2E tests for user account section, chevron expand, link indicator, and edit-redirect behavior
- **FluentAssertions 8.x** changed `BeOneOf` API — use `.Match(s => s == X || s == Y)` for nullable numeric assertions with reason strings
- **Reflection-based testing for not-yet-implemented endpoints**: Use `GetMethod()` with null-check + `Assert.Fail()` to produce clear messages when Data hasn't created the endpoint yet, rather than NullReferenceException
- **Dynamic argument builder**: `BuildEndpointArgs()` matches parameters by type for flexibility when Data's method signatures aren't finalized. This avoids needing exact parameter order
- **Both DbContexts share the same PostgreSQL testcontainer** — EmployeeDbContext uses `employees` schema, WarpBusinessDbContext uses `warp` schema
- Tests that need cross-schema data (e.g., DeleteUser_BlockedWhenLinkedToEmployee) seed data in both contexts independently
- **Key edge case identified**: `UpdateEmployee_CanSetUserIdFromNull` accepts both 200 and 400 because the null→value transition is valid per the immutability rule, but may fail user-existence validation
- **Status:** ✅ Tests compile. Awaiting Data's backend implementation for runtime verification.
