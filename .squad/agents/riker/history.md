# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, PostgreSQL
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-11: Initial .NET Aspire Architecture

**Solution Structure:**
- `WarpBusiness.sln` - Root solution file
- `WarpBusiness.AppHost` - Aspire orchestrator project, coordinates all resources and services
- `WarpBusiness.ServiceDefaults` - Shared Aspire configuration (health checks, telemetry, resilience)
- `WarpBusiness.Api` - ASP.NET Core Web API (middle tier)
- `WarpBusiness.Web` - Blazor Web App (frontend)

**Architecture Patterns:**
- Aspire orchestration pattern: AppHost references and manages all projects
- ServiceDefaults pattern: Shared health endpoints, telemetry, and service discovery configuration
- Project references flow: AppHost → {ServiceDefaults, Api, Web}; Api → ServiceDefaults; Web → ServiceDefaults
- Database integration: PostgreSQL added as Aspire resource with PgAdmin for management
- Service communication: Web references Api for service-to-service calls

**Key Files:**
- `WarpBusiness.AppHost\AppHost.cs` - Orchestration configuration (PostgreSQL, API, Web wiring)
- `WarpBusiness.Api\Program.cs` - API startup with ServiceDefaults integration
- `WarpBusiness.Web\Program.cs` - Blazor startup with ServiceDefaults integration

**Technical Decisions:**
- Using .NET 10 SDK (10.0.201) with Aspire 13.2.2 (NuGet packages, not workload)
- PostgreSQL as the database with PgAdmin included for development
- MapDefaultEndpoints() called in both API and Web for health checks/telemetry
- WithExternalHttpEndpoints() on Web project for external access
- WithPgAdmin() on PostgreSQL resource for database management UI

### 2026-07-07: TenantPortal Scaffolding

**What was done:**
- Created `WarpBusiness.TenantPortal` project (Blazor Server + OIDC, Keycloak client: `warpbusiness-tenant-portal`)
- Registered in Aspire AppHost with `WithReference(api)` and `WithReference(keycloak)`
- Added to `WarpBusiness.slnx` solution file
- Added project reference in `WarpBusiness.AppHost.csproj`

**Architecture decisions confirmed:**
- `TenantRequest` stays in warp schema (WarpBusinessDbContext) — tightly coupled to Tenant
- `LogoBase64`/`LogoMimeType` stay directly on Tenant entity — simplicity over normalization
- Subscription metadata (`MaxUsers`, `SubscriptionPlan`, `EnabledFeatures`) stays on Tenant — no need for separate table yet
- Port allocation: 5080 (http) / 7080 (https) — continues portal numbering convention
- Keycloak client: `warpbusiness-tenant-portal` — follows naming pattern

**Existing scaffold found:**
- Project already had partial scaffolding (Program.cs, services, some pages with full implementations)
- Updated `_Imports.razor` to include auth-related usings and `Microsoft.JSInterop`
- Updated `Routes.razor` to use `CascadingAuthenticationState` and `Layout.MainLayout`
- Removed duplicate root-level `Components/MainLayout.razor` in favor of `Components/Layout/MainLayout.razor`
- Created `Components/Layout/NavMenu.razor` as separate component

**Follow-up needed:**
- Create `warpbusiness-tenant-portal` client in Keycloak realm JSON
- API endpoints for tenant self-service may need new routes
- Signup flow needs API support for anonymous tenant registration

### 2026-07-14: MinIO Image Storage Architecture

**Architecture Decisions Made:**
- SDK: `Minio` NuGet package (official MinIO .NET SDK) — direct, idiomatic, no S3 compatibility shim needed
- Bucket: Single `warp-catalog` bucket with key prefix for tenant isolation
- Object key format: `{tenantId}/products/{productId}/{uuid}.{ext}` — immutable, tenant-scoped
- Upload flow: Client → API → MinIO (API as middleman) — simpler, validates auth/tenant before store
- Image serving: API proxy endpoint (`GET /api/catalog/images/{key*}`) — keeps MinIO internal
- Model changes: `ImageKey` nullable string on Product and ProductVariant — no separate entity

**Key File Paths:**
- `WarpBusiness.Catalog/Models/Product.cs` — add ImageKey property
- `WarpBusiness.Catalog/Models/ProductVariant.cs` — add ImageKey property
- `WarpBusiness.Catalog/Data/CatalogDbContext.cs` — no index needed for ImageKey (nullable, not unique)
- `WarpBusiness.AppHost/AppHost.cs` — add MinIO container wiring
- `WarpBusiness.Api/Endpoints/CatalogEndpoints.cs` — add image upload/proxy endpoints
- `WarpBusiness.Api/Services/MinioService.cs` — new service for MinIO operations

**Rationale:**
- Minio SDK chosen over AWSSDK.S3 for direct MinIO features and simpler config
- Single bucket with tenant prefix > bucket-per-tenant for operational simplicity
- API proxy chosen to keep MinIO non-public and leverage existing auth
- Deferred multi-image support (ProductImage entity) until actually needed

### 2026-07-14: WarpBusiness.Storage Library Architecture

**Architecture Decision Written:** `.squad/decisions/inbox/riker-storage-library.md`

**Key Decisions:**
- SDK: `Minio` NuGet (client) + `CommunityToolkit.Aspire.Hosting.Minio` (hosting) — clean Aspire integration
- Buckets: Resource-type buckets (`warp-catalog`, `warp-logos`, `warp-documents`) not tenant buckets
- Object keys: `{tenantId}/{resourceType}/{resourceId}/{uuid}.{ext}` pattern
- CORS: Configure via MinIO API at API startup, not container init scripts

**Interface Designed:**
- `IFileStorageService` with: `UploadAsync`, `DownloadAsync`, `GetPresignedUrlAsync`, `DeleteAsync`, `EnsureBucketExistsAsync`, `ExistsAsync`
- Both Stream download and presigned URL — caller decides based on use case

**Project Structure:**
- New library: `WarpBusiness.Storage/` (interface + MinIO implementation)
- No DbContext — storage is stateless; image keys stored on existing entities
- DI helper: `AddMinioStorage()` extension method

**Gotchas Documented:**
- Bucket creation should be lazy (first upload) not startup — MinIO may not be ready
- CORS must be configured before first presigned PUT from browser
- Always pass explicit contentType on upload

### 2026-07-14: Taxonomy v2 + Catalog Variant Architecture (Clean Slate)

**Architecture Document Written:** `~/.copilot/session-state/.../files/taxonomy-v2-architecture.md`
**Decision Written:** `.squad/decisions/inbox/riker-taxonomy-v2-architecture.md`
**Supersedes:** taxonomy-architecture.md (2026-04-15), copilot-directive-taxonomy-clean-slate.md

**Context:**
Michael's new vision: shared marketplace taxonomy reference data + multi-marketplace product mapping + variant generation from attribute combinations. **Both** the old WarpBusiness.Taxonomy module AND the old EAV-based catalog attribute system are fully replaced (clean slate — no migration, no coexistence).

**Key Decisions:**

1. **New `common_taxonomy` schema (shared, NOT tenant-scoped):**
   - `TaxonomyProvider` table: google, amazon, etsy, ebay, newegg with download tracking
   - `TaxonomyNode` table: hierarchical tree per provider (adjacency list + materialized path)
   - `TaxonomyNodeAttribute` table: per-node attributes with types, valid values (jsonb), variant-axis hints

2. **New `WarpBusiness.CommonTaxonomy` project** replaces existing `WarpBusiness.Taxonomy`
   - Own `CommonTaxonomyDbContext` on `common_taxonomy` schema
   - `ITaxonomyDownloader` returns both nodes AND attributes
   - `IFileTaxonomyDownloader` for Amazon/Newegg (no public APIs)
   - `TaxonomyDownloadBackgroundService` (weekly, configurable)
   - `TaxonomyDownloadOrchestrator` with checksum-based change detection

3. **Catalog schema — clean-slate EAV replacement:**
   - **DELETED:** `CatalogAttributeType`, `CatalogAttributeOption`, `ProductType`, `ProductTypeAttribute`, `ProductVariantAttributeValue`, `AttributeValueType`
   - **NEW:** `ProductOption` (per-product, Shopify-style), `ProductOptionValue`, `VariantOptionValue`, `OptionValueType`
   - **NEW:** `ProductTaxonomyMapping`: product → marketplace node (one per provider per product)
   - **NEW:** `ProductTaxonomyAttributeValue`: marketplace-specific product attributes (material, gender, etc.)
   - **MODIFIED:** `Product` — remove `ProductTypeId`, add `Options` nav; `ProductVariant` — `OptionValues` replaces `AttributeValues`

4. **Variant computation (Shopify pattern):**
   - Explicit variant rows (materialized Cartesian product)
   - `VariantGenerationService` computes Color×Size combinations → variant rows
   - Price: `Product.BasePrice` + `ProductVariant.Price` override (already exists)

5. **Cross-schema design:** Raw GUID references from catalog → common_taxonomy (no EF navigation cross-schema)

6. **Industry alignment:** Analyzed Shopify, Google Shopping, Amazon, eBay, Etsy, GS1/GPC models. Recommended Shopify variant model + Google attribute model as the simplest satisfying combination.

**Open Questions for Michael:**
- Variant count limit? (recommend 200 max)
- Amazon/Newegg in v2 or defer?
- Relative price adjustments vs. absolute overrides only?

### 2026-04-15: Warp Taxonomy System Architecture

**Architecture Document Written:** `~/.copilot/session-state/.../files/taxonomy-architecture.md`
**Decision Written:** `.squad/decisions/inbox/riker-taxonomy-architecture.md`

**Context:**
Michael requested product taxonomy with external imports from Amazon, Newegg, and Etsy.

**Key Findings & Decisions:**

1. **External Providers Changed:** Amazon and Newegg have no public taxonomy APIs. Replaced with:
   - **Google Product Taxonomy** — public URL, no auth, industry standard (~6,000 categories)
   - **eBay Taxonomy API** — OAuth app token, free tier (5,000/day)
   - **Etsy Taxonomy API** — API key required, free tier available

2. **New Module:** `WarpBusiness.Taxonomy` following existing module pattern
   - Schema: `taxonomy`
   - Entities: `TaxonomyNode` (per-tenant), `ExternalTaxonomyCache`, `ExternalTaxonomyNode`

3. **Data Model:** Adjacency list + MaterializedPath
   - `ParentNodeId` for CRUD and navigation properties
   - `MaterializedPath` (e.g., `/001/003/007`) for breadcrumb display
   - `Level` cached to avoid recursion

4. **Import Tracking:** On `TaxonomyNode`:
   - `SourceProvider` (enum: Google, eBay, Etsy)
   - `SourceExternalId` (original ID from provider)
   - `SourceImportedAt` (timestamp)
   - Enables future re-sync and audit trail

5. **Separate External Cache:** `ExternalTaxonomyNode` table stores downloaded external nodes
   - Keeps external data separate from tenant data
   - Enables side-by-side comparison
   - Change detection via checksum comparison

6. **Downloader Interface:** `ITaxonomyDownloader` with per-provider implementations
   - `GoogleTaxonomyDownloader` — parses plain text format
   - `EbayTaxonomyDownloader` — OAuth + JSON tree traversal
   - `EtsyTaxonomyDownloader` — API key + JSON tree traversal

7. **UI Pages:**
   - `/catalog/taxonomy` — manage Warp taxonomy (tree CRUD)
   - `/catalog/taxonomy/import` — browse external, select nodes, import

**Files to Create:**
- `WarpBusiness.Taxonomy/` project (17+ files)
- `Taxonomy.razor`, `TaxonomyImport.razor` UI pages
- `TaxonomyApiClient.cs` HTTP client

**Files to Modify:**
- `WarpBusiness.slnx` — add project
- `WarpBusiness.Api/Program.cs` — register DbContext, services, endpoints
- `WarpBusiness.Api.csproj` — add project reference
- `NavMenu.razor` — add navigation links
