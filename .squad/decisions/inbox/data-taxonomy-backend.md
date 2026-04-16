# Taxonomy Backend Module Decision

**Date:** 2026-04-16  
**Agent:** Data (Backend Dev)  
**Status:** ✅ Implemented

## Context

Michael requested the full WarpBusiness.Taxonomy backend module based on the taxonomy architecture reference, with Amazon restored as a provider and all API endpoints wired into the API project.

## Decision

### 1. New Taxonomy Module

Created `WarpBusiness.Taxonomy` as a standalone class library following the Catalog/CRM module pattern:

- **Schema:** `taxonomy`
- **DbContext:** `TaxonomyDbContext` with `HasDefaultSchema("taxonomy")`
- **Initializer:** `TaxonomyDbInitializer` runs `MigrateAsync` on startup

### 2. Data Model

Implemented the three core entities:

- **TaxonomyNode** (per-tenant Warp taxonomy; adjacency list + materialized path)
- **ExternalTaxonomyCache** (download metadata per provider)
- **ExternalTaxonomyNode** (cached external nodes)

Enums are persisted as strings and `SourceProvider + SourceExternalId` is enforced as a filtered unique index.

### 3. Providers

Supported providers match the updated requirements:

- **Google** (public download, no auth)
- **Amazon** (PA-API 5.0, credential-gated via AccessKeyId/SecretAccessKey/AssociateTag)
- **eBay** (OAuth app token)
- **Etsy** (API key)

### 4. Services

- **TaxonomyDownloadService** orchestrates downloads and cache updates.
- **TaxonomyImportService** handles idempotent imports, ancestor creation, and slug-based materialized path generation.

### 5. API Endpoints

`TaxonomyEndpoints` implements full CRUD, external browsing/search, and import/preview endpoints. Tenant context is enforced via `HttpContext.Items["TenantId"]`.

### 6. API Registration

`WarpBusiness.Api` registers:

- Taxonomy DbContext + initializer
- HttpClient registrations for each provider
- Downloader + orchestration services
- `app.MapTaxonomyEndpoints()`

## Files Created

- `WarpBusiness.Taxonomy/` (project, models, data, services, endpoints)
- `WarpBusiness.Taxonomy/Data/Migrations/*` (InitialTaxonomySchema)

## Files Modified

- `WarpBusiness.Api/WarpBusiness.Api.csproj`
- `WarpBusiness.Api/Program.cs`
- `WarpBusiness.slnx`

## Testing

- ✅ `dotnet build WarpBusiness.Taxonomy`
- ✅ `dotnet build WarpBusiness.Api`

## Notes

- Materialized paths use slugged name segments (`/segment1/segment2/...`) for readability.
- Amazon downloader is credential-gated and returns a friendly error if missing config.
