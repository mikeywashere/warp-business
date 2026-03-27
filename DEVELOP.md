# Developer Guide

This guide explains how to run Warp Business locally for development. The project supports three development approaches: .NET Aspire (recommended for full stack), Visual Studio, and VS Code. Choose based on your preference; all three approaches manage services, databases, and dependencies automatically.

## Prerequisites

Install the following before starting:

- **.NET 10 SDK** — [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Docker Desktop** — [Download here](https://www.docker.com/products/docker-desktop) (required for Aspire and PostgreSQL)
- **PostgreSQL 16** — Runs automatically via Aspire; no separate install needed for local dev
- **Node.js** (optional) — Only if working with frontend tooling beyond Blazor
- **Visual Studio 2022 17.x or later** (optional) — Recommended for Windows development
- **VS Code with C# Dev Kit extension** (optional) — Lightweight alternative to Visual Studio
- **kubectl** — [Download here](https://kubernetes.io/docs/tasks/tools/) (required for Kubernetes deployment)
- **skaffold** — [Download here](https://skaffold.dev/docs/install/) (required for Kubernetes dev workflow)
- **GitHub CLI `gh`** (optional) — [Download here](https://cli.github.com/) (useful for PR workflows)

### Quick Install

Run the appropriate script for your platform to install all required tools:

**Linux / macOS:**
```bash
bash scripts/install-prerequisites.sh
```

**Windows (PowerShell):**
```powershell
.\scripts\install-prerequisites.ps1
```

> **Note:** Visual Studio and VS Code are not installed by these scripts — install them manually from [visualstudio.microsoft.com](https://visualstudio.microsoft.com) or [code.visualstudio.com](https://code.visualstudio.com).
>
> After running the script, **restart your terminal** so PATH changes take effect before running `dotnet` or other newly installed tools.

## Project Structure

The solution is organized as follows:

```
src/
├── WarpBusiness.AppHost/              # .NET Aspire orchestrator (entry point for local dev)
├── WarpBusiness.Api/                  # Backend API (ASP.NET Core 10, EF Core, PostgreSQL)
├── WarpBusiness.Web/                  # Frontend (Blazor Server)
├── WarpBusiness.CustomerPortal/       # External customer portal (separate container)
├── WarpBusiness.ServiceDefaults/      # Shared telemetry and health check configuration
├── WarpBusiness.Shared/               # Shared DTOs and domain models
├── WarpBusiness.Plugin.Abstractions/  # Plugin interface contracts
├── WarpBusiness.Plugin.Crm/           # CRM plugin (built-in)
├── WarpBusiness.Plugin.EmployeeManagement/  # Employee Management plugin (built-in)
├── WarpBusiness.Plugin.Sample/        # Example plugin
└── WarpBusiness.Tests/                # xUnit integration tests
k8s/                                   # Kubernetes manifests
├── api/                               # API deployment
├── web/                               # Web app deployment
├── portal/                            # Customer portal deployment
├── postgres/                          # PostgreSQL StatefulSet
├── keycloak/                          # Keycloak deployment (optional)
└── secrets.yaml.template              # Secrets template (copy and fill in values)
docs/                                  # Additional documentation
├── auth-providers.md                  # Authentication configuration guide
├── kubernetes-deployment.md           # Kubernetes deployment guide
├── plugin-development.md              # Plugin development guide
└── adr/                               # Architecture Decision Records
```

## Running Locally with .NET Aspire (Recommended)

.NET Aspire is the recommended way to develop Warp Business. It orchestrates all services automatically, handles service discovery, injects connection strings, and provides the Aspire Dashboard for real-time monitoring.

### Quick Start

```bash
cd src
dotnet run --project WarpBusiness.AppHost
```

Aspire will start all services:
- **PostgreSQL database** (via Docker)
- **WarpBusiness.Api** — Backend API
- **WarpBusiness.Web** — Blazor Server frontend
- **WarpBusiness.CustomerPortal** — Customer portal
- **Aspire Dashboard** — http://localhost:15888 (typically, port may vary)

The dashboard shows all running services, their health status, logs, traces, and metrics.

### What Aspire Provides Automatically

When using Aspire, you do **not** need to manually configure:

- **Database connection strings** — Auto-injected via service discovery; PostgreSQL connection is created on startup
- **Service URLs** — Services discover each other automatically (e.g., `https+http://api` in the Web app)
- **JWT signing key** — Set to `dev-only-secret-key-32-chars-minimum!!` in Development environment
- **Port assignments** — Automatically assigned and can be viewed in the dashboard

Just run the AppHost and everything is wired up.

## Running with Visual Studio 2022

Visual Studio provides integrated debugging and full IDE support.

1. **Open the solution** — Open `src\WarpBusiness.sln` in Visual Studio 2022 (17.x or later)
2. **Ensure Aspire workload is installed** — Run `dotnet workload install aspire` (one-time setup)
3. **Set startup project** — Right-click `WarpBusiness.AppHost` → "Set as Startup Project"
4. **Start debugging** — Press **F5** (Debug) or **Ctrl+F5** (Run without debugging)
5. **Aspire Dashboard opens automatically** — Visual Studio will open the dashboard in a browser window

Debug breakpoints work across all services. Stop debugging to shut down all services.

## Running with VS Code

VS Code with the C# Dev Kit extension provides a lightweight development environment.

1. **Open the folder** — Open `src/` in VS Code
2. **Install extensions** — Install "C# Dev Kit" and "Docker" from the Extensions marketplace (VS Code should prompt you)
3. **Open a terminal** — Use View → Terminal (Ctrl+`)
4. **Run Aspire** — Type:
   ```bash
   dotnet run --project WarpBusiness.AppHost
   ```
5. **Access the dashboard** — Open http://localhost:15888 in your browser

If `.vscode/launch.json` exists, you can also use the Run/Debug view (Ctrl+Shift+D) to launch with a predefined configuration.

## Running Individual Services (Without Aspire)

For debugging a single service (e.g., just the API), you can run services individually. This requires manual environment variable setup.

### Running the API Alone

```bash
cd src/WarpBusiness.Api
dotnet run
```

Before running, ensure these environment variables are set:

```bash
# JWT Configuration
ASPNETCORE_ENVIRONMENT=Development
Jwt__Key=dev-only-secret-key-32-chars-minimum!!
Jwt__Issuer=WarpBusiness.Api
Jwt__Audience=WarpBusiness.Web

# Database (PostgreSQL running on localhost:5432)
ConnectionStrings__DefaultConnection=User ID=postgres;Password=OhHowSad6;Host=localhost;Port=5432;Database=myDataBase;Pooling=true;

# Auth Provider
AuthProvider__ActiveProvider=Local
```

### Running the Web App Alone

```bash
cd src/WarpBusiness.Web
dotnet run
```

The Web app connects to the API. Before running, ensure it can reach the API (set the API URL in service discovery or appsettings).

---

## Environment Variables & Configuration

### Development (Aspire-Managed)

When using Aspire, all these variables are automatically set and injected. You don't need to configure them manually:

| Variable | Description | Aspire Default |
|----------|-------------|-----------------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Development` |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection (auto-injected) | Auto-generated by Aspire |
| `Jwt__Key` | JWT signing key (min 32 chars) | `dev-only-secret-key-32-chars-minimum!!` |
| `Jwt__Issuer` | JWT issuer claim | `WarpBusiness.Api` |
| `Jwt__Audience` | JWT audience claim | `WarpBusiness.Web` |
| `Jwt__AccessTokenExpiryMinutes` | Bearer token lifetime | `60` (dev), `15` (production) |
| `Jwt__RefreshTokenExpiryDays` | Refresh token lifetime | `30` (dev), `7` (production) |
| `AuthProvider__ActiveProvider` | Active authentication provider | `Local` |

### Manual Configuration (Non-Aspire Deployments)

If running services individually without Aspire, create an `appsettings.Development.json` file in each service root or set environment variables manually.

Example `appsettings.Development.json` for `WarpBusiness.Api`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Jwt": {
    "Key": "dev-only-secret-key-32-chars-minimum!!",
    "Issuer": "WarpBusiness.Api",
    "Audience": "WarpBusiness.Web",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 30
  },
  "ConnectionStrings": {
    "DefaultConnection": "User ID=postgres;Password=OhHowSad6;Host=localhost;Port=5432;Database=warpbusiness;Pooling=true;"
  },
  "AuthProvider": {
    "ActiveProvider": "Local"
  }
}
```

### Authentication Providers

Warp Business supports three authentication modes. Choose one by setting `AuthProvider__ActiveProvider` in configuration.

#### 1. Local (Default)

Local authentication uses ASP.NET Core Identity with JWT tokens. No additional configuration needed.

- Users register via `/api/auth/register`
- Login via `/api/auth/login`
- Passwords are hashed with bcrypt
- No external service required

#### 2. Keycloak (OIDC)

To use Keycloak:

1. Ensure Keycloak is running (Aspire provides a local Keycloak instance at port 8080)
2. Set configuration in `appsettings.Development.json`:

```json
{
  "AuthProvider": {
    "ActiveProvider": "Keycloak",
    "Keycloak": {
      "Authority": "https://localhost:8080/realms/warpbusiness",
      "ClientId": "warpbusiness-api",
      "Audience": "warpbusiness-api",
      "EmailClaim": "email",
      "NameClaim": "name"
    }
  }
}
```

3. First login via OIDC will auto-provision a shadow `ApplicationUser` in the database

#### 3. Microsoft Azure AD (OIDC)

To use Microsoft Azure AD:

1. Register an app in Azure AD
2. Set configuration:

```json
{
  "AuthProvider": {
    "ActiveProvider": "Microsoft",
    "Microsoft": {
      "TenantId": "your-tenant-id",
      "ClientId": "your-client-id",
      "Audience": "api://your-client-id"
    }
  }
}
```

Only one provider can be active at a time. To switch, update the configuration and restart.

For detailed authentication setup, see [docs/auth-providers.md](docs/auth-providers.md).

### Plugin Configuration

Warp Business has a modular plugin system for extending functionality.

#### Built-in Plugins (Development)

In development, plugins are loaded from project references:
- `WarpBusiness.Plugin.Crm` — CRM module (Contacts, Companies, Deals, Activities)
- `WarpBusiness.Plugin.EmployeeManagement` — Employee management

#### Custom Plugins (Deployment)

To create a custom plugin:

1. Create a DLL that implements `IWarpPlugin` from `WarpBusiness.Plugin.Abstractions`
2. Place the DLL in a `plugins/` directory next to the API executable
3. The API will discover and load plugins on startup

Plugin loading happens automatically; check API startup logs for discovery messages.

For detailed plugin development, see [docs/plugin-development.md](docs/plugin-development.md).

---

## Running Tests

Warp Business uses **xUnit** for unit and integration tests.

### Run All Tests

```bash
cd src
dotnet test WarpBusiness.sln
```

### Run Tests for a Specific Project

```bash
dotnet test WarpBusiness.Tests/
```

### Run Tests with Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports are generated in the `TestResults/` directory.

### Test Strategy

- **Integration tests** use `WebApplicationFactory<Program>` with an in-memory EF Core database
- Each test class gets its own isolated database instance
- The test factory (`WarpTestFactory`) auto-injects test JWT settings
- Use `AuthHelper` for test authentication: `await factory.CreateAuthenticatedClient()`

See `.squad/decisions.md` for the full integration test strategy.

---

## Database Migrations

Warp Business uses **Entity Framework Core** with PostgreSQL. Migrations are version-controlled.

### Apply Migrations

In development with Aspire, migrations run automatically on startup. If needed manually:

```bash
cd src
dotnet ef database update --project WarpBusiness.Api
```

### Create a New Migration

```bash
cd src
dotnet ef migrations add {MigrationName} --project WarpBusiness.Api
```

For example:
```bash
dotnet ef migrations add AddContactFields --project WarpBusiness.Api
```

### Check Migration Status

```bash
dotnet ef migrations list --project WarpBusiness.Api
```

### Remove Last Migration (Before Commit)

```bash
dotnet ef migrations remove --project WarpBusiness.Api
```

**Important**: Commit migrations to git with your code changes so the database schema stays synchronized across the team.

---

## Kubernetes (Local Cluster)

For local Kubernetes development, use a local cluster (Docker Desktop, kind, or minikube). Detailed setup is in [docs/kubernetes-deployment.md](docs/kubernetes-deployment.md).

### Quick Start (Docker Desktop)

Docker Desktop includes a single-node Kubernetes cluster. Enable it in **Docker Desktop → Settings → Kubernetes → Enable Kubernetes**.

Then deploy:

```bash
# Build Docker images
make build

# Deploy to Kubernetes
make deploy

# Check status
make status

# View API logs
make logs-api

# Tear down
make undeploy
```

### Required: Create Secrets File

Before deploying to Kubernetes, create `k8s/secrets.yaml` from the template:

```bash
cp k8s/secrets.yaml.template k8s/secrets.yaml
```

Edit `k8s/secrets.yaml` and fill in:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: warp-secrets
  namespace: warp-business
type: Opaque
stringData:
  postgres-password: "secure-password-here"
  jwt-key: "your-32-char-minimum-jwt-key!!"
  # Add any auth provider credentials (OAuth client secrets, etc.)
```

**Important**: Never commit `k8s/secrets.yaml` to git (it's in `.gitignore`). Always use the template and fill in locally.

### Make Targets (Available Commands)

| Command | Description |
|---------|-------------|
| `make build` | Build all Docker images (API, Web, Portal) |
| `make load-kind` | Load images into kind cluster |
| `make build-minikube` | Build images for minikube |
| `make deploy` | Deploy to Kubernetes (requires `k8s/secrets.yaml`) |
| `make undeploy` | Remove all K8s resources |
| `make status` | Show pods, services, and ingress status |
| `make logs-api` | Tail API logs |
| `make logs-web` | Tail Web app logs |
| `make logs-portal` | Tail Customer Portal logs |
| `make restart` | Rolling restart all deployments |
| `make clean` | Delete entire namespace and PVCs |

### Skaffold (Alternative to Make)

Skaffold automates building, pushing, and deploying to Kubernetes:

```bash
# One-time setup: select cluster
kubectl config use-context docker-desktop  # or kind, minikube, etc.

# Run dev mode (watch files, rebuild on changes)
skaffold dev

# Run once
skaffold run

# Delete resources
skaffold delete
```

Skaffold config is in `skaffold.yaml`. It:
- Watches source files
- Rebuilds Docker images on changes
- Auto-redeploys to Kubernetes
- Port-forwards services to localhost (API → 5001, Web → 5002, Portal → 5003)

---

## Common Issues & Troubleshooting

### Docker Not Running

**Error:** "Cannot connect to Docker daemon" or "Docker Desktop not running"

**Fix:**
1. Start Docker Desktop
2. Wait 30 seconds for Docker to fully initialize
3. Run `docker ps` to verify

Aspire requires Docker to be running to start PostgreSQL in a container.

### Port Conflicts

**Error:** "Address already in use" or "bind: address already in use"

**Check running services:**
```bash
# Windows
netstat -ano | findstr :5001

# macOS/Linux
lsof -i :5001
```

Default ports used:
- API: 5001
- Web: 5002
- Customer Portal: 5003
- Aspire Dashboard: 15888
- Keycloak: 8080
- PostgreSQL: 5432

**Fix:** Change port in `appsettings.Development.json` or Aspire Program.cs, or stop the conflicting process.

### Database Connection Refused

**Error:** "Npgsql.NpgsqlException: could not translate host name 'localhost' to address"

**Check:**
1. Is Docker running? (Aspire spins up PostgreSQL in Docker)
2. Is the PostgreSQL container healthy? Run `docker ps | grep postgres`
3. Connection string correct? Check `appsettings.Development.json`

**Fix:**
- With Aspire: Just run the AppHost; Aspire starts PostgreSQL automatically
- Without Aspire: Ensure PostgreSQL is running on port 5432

### JWT Errors (401 Unauthorized)

**Error:** "Invalid token" or "401 Unauthorized" responses

**Check:**
1. `Jwt__Key` is set and at least 32 characters
2. Token not expired: `Jwt__AccessTokenExpiryMinutes` is correct
3. Token includes correct `aud` (audience) claim

**Fix:**
```json
{
  "Jwt": {
    "Key": "dev-only-secret-key-32-chars-minimum!!",
    "Issuer": "WarpBusiness.Api",
    "Audience": "WarpBusiness.Web",
    "AccessTokenExpiryMinutes": 60
  }
}
```

### Service Discovery Failures

**Error:** "Cannot resolve service 'api'" or similar

**Cause:** When using Aspire, services discover each other by name. If a service isn't running or crashes, references fail.

**Check:**
1. Open Aspire Dashboard (http://localhost:15888)
2. Look for red/orange service indicators
3. Check that all services have green "Running" status

**Fix:** Stop and restart the AppHost with `dotnet run --project WarpBusiness.AppHost`

### Plugin Not Loading

**Error:** Plugin doesn't appear in logs or functionality is missing

**Check:**
1. Is the plugin DLL in the `plugins/` folder?
2. Does it implement `IWarpPlugin`?
3. Check API startup logs for plugin discovery messages

**Fix:**
1. Ensure plugin DLL is next to the API executable (for manual deployments)
2. In development, ensure project is referenced in AppHost
3. Restart the API service

### Tests Failing Due to Database State

**Error:** Test fails with foreign key constraint or duplicate key error

**Cause:** Test isolation issue or database not reset between tests

**Fix:**
```csharp
// In test setup, use a unique database name per test class:
var dbName = $"TestDb_{Guid.NewGuid()}";
// This ensures each test class gets a fresh database
```

### Cannot Access Web App from Browser

**Error:** "localhost:5002 refused to connect" or blank page

**Check:**
1. Is the Web service running? Check Aspire Dashboard
2. Is Blazor server started? Check console output for "Started server"
3. Browser console for JavaScript errors

**Fix:**
1. Hard refresh browser (Ctrl+Shift+R / Cmd+Shift+R)
2. Clear browser cache
3. Check that API is accessible (open http://localhost:5001/health)

### Keycloak Connection Issues

**Error:** "Authority is not reachable" when using Keycloak authentication

**Check:**
1. Is Keycloak running? (Aspire starts it automatically)
2. Is the Authority URL correct in `appsettings.Development.json`?
3. Can you reach Keycloak? Try http://localhost:8080 in browser

**Fix:**
- If using Aspire: Just run the AppHost; Keycloak starts automatically
- If manual: Ensure Keycloak is running and Authority URL is correct

---

## Next Steps

- Read [docs/auth-providers.md](docs/auth-providers.md) for detailed authentication setup
- Read [docs/kubernetes-deployment.md](docs/kubernetes-deployment.md) for production K8s deployment
- Read [docs/plugin-development.md](docs/plugin-development.md) for plugin development
- Check `.squad/decisions.md` for architectural decisions and team patterns
- Explore `.vscode/` for recommended VS Code settings and launch configurations

Happy developing! 🚀
