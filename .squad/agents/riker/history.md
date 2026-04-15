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
