# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** Warp Business — Business Management System (CRM first)
- **Stack:** .NET 10, Blazor (frontend), ASP.NET Core Web API (backend), PostgreSQL, Entity Framework Core, Auth/Authz
- **Role:** Backend Dev — APIs, services, EF Core, domain logic
- **Created:** 2026-03-25

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-25: CRM Domain Model Implemented

- **Domain Entities:** Created Contact, Company, Deal, and Activity entities with proper relationships
  - Contact → Company (many-to-one, nullable)
  - Contact ↔ Deal (many-to-many via navigation properties)
  - Contact ↔ Activity (one-to-many)
  - Company ↔ Deal (one-to-many)
  - Deal ↔ Activity (one-to-many)
- **EF Core Configurations:** Built IEntityTypeConfiguration classes for all CRM entities with explicit column constraints, indexes, and delete behaviors (SetNull for soft relationships)
- **Service Layer Pattern:** Implemented thin controllers with full business logic in ContactService. All data access is async with CancellationToken support.
- **DTOs:** Created record-based DTOs in WarpBusiness.Shared.Crm for clean API contracts. PagedResult<T> pattern established for list endpoints with built-in pagination metadata.
- **ApplicationDbContext:** Successfully integrated CRM DbSets into Bishop's ApplicationDbContext. Used ApplyConfigurationsFromAssembly for automatic configuration discovery.
- **Standards:**
  - All service methods are async and accept CancellationToken
  - Query operations use AsNoTracking() for performance
  - CreatedBy and OwnerId fields link to ApplicationUser.Id (string, max 450 chars for EF Identity compatibility)
  - DateTimeOffset used consistently for temporal data

### 2026-03-25: Companies and Deals Services Implemented

- **Companies service pattern:** `ICompanyService` / `CompanyService` mirrors Contacts exactly. `DeleteCompanyAsync` returns a `DeleteCompanyResult` enum (`Deleted`, `NotFound`, `HasContacts`) to give the controller enough signal for 204/404/409 without leaking business logic into the HTTP layer.
- **409 Conflict handling:** `DeleteCompanyAsync` loads the company with `.Include(c => c.Contacts)` and short-circuits with `HasContacts` if any exist — contacts are never orphaned. The controller maps this to `Conflict(new { message = ... })`.
- **Deals service pattern:** `IDealService` / `DealService` — list endpoint supports both search (title contains) and stage filter. Stage filter uses `Enum.TryParse` with `ignoreCase: true`; unknown values are silently skipped (no 400).
- **Pipeline summary query:** `GetDealSummaryAsync` groups `_db.Deals` by `Stage` in a single EF `GroupBy` → `Select`, then excludes `ClosedLost` from `TotalPipelineValue` client-side after the DB round-trip.
- **ContactName in DealDto:** Used `d.Contact.FirstName + " " + d.Contact.LastName` in Select projections — avoids translating the unmapped `FullName` computed property through a navigation.
- **Routing:** `GET /api/deals/summary` is declared before `GET /api/deals/{id:guid}` to avoid routing conflicts with the GUID constraint.
- **OwnerId:** Set from the JWT `sub` / `NameIdentifier` claim at the controller layer, passed down to `CreateDealAsync(request, userId)`.
