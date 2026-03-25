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
