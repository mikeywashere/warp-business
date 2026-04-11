# Skill: ASP.NET Core Multi-Tenancy (Header-Based)

## Pattern

Shared-database multi-tenancy using an HTTP header (`X-Tenant-Id`) for tenant context, with EF Core and minimal APIs.

## When to Use

- Multiple organizations share a single deployment and database
- Users may belong to multiple tenants (many-to-many)
- Row-level data isolation is sufficient (no separate databases per tenant)

## Implementation Steps

### 1. Models

- `Tenant` entity with `Id`, `Name`, `Slug` (unique), `IsActive`, timestamps
- `UserTenantMembership` join entity with composite key (`UserId` + `TenantId`)
- Navigation properties on both `ApplicationUser` and `Tenant`

### 2. DbContext

- Add `DbSet<Tenant>` and `DbSet<UserTenantMembership>`
- Configure composite key: `entity.HasKey(e => new { e.UserId, e.TenantId })`
- Unique index on `Tenant.Slug`
- Cascade delete on both FK relationships

### 3. Middleware (in Program.cs, after auth)

```csharp
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var header)
        && Guid.TryParse(header, out var tenantId)
        && context.User.Identity?.IsAuthenticated == true)
    {
        // Validate tenant exists and user is a member (or is admin)
        // Set: context.Items["TenantId"] = tenantId;
    }
    await next();
});
```

### 4. Exempt Paths

Certain paths work without a tenant header:
- User profile (`/api/users/me`)
- Tenant selection (`/api/users/me/tenants`, `/api/users/me/tenant`)
- Tenant management (`/api/tenants`)
- Health checks

### 5. Tenant-Aware Queries

In endpoints, read `httpContext.Items["TenantId"]` and filter data accordingly. Admins without a tenant header see all data.

### 6. Frontend Flow

1. After login, call `GET /api/users/me/tenants`
2. If multiple, show tenant selector
3. Store selected tenant ID, send as `X-Tenant-Id` header on all requests

## Key Decisions

- **Header-based** (not subdomain/path-based) — simplest for SPAs
- **Global roles** — admin is platform-wide, not per-tenant
- **Stateless** — no server-side tenant session; header on every request
