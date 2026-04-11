# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, Entity Framework Core, PostgreSQL
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Keycloak Authentication (2026-04-11)

- **Aspire Keycloak packages** are preview-only at 13.2.2: use version `13.2.2-preview.1.26207.2` for both `Aspire.Hosting.Keycloak` (AppHost) and `Aspire.Keycloak.Authentication` (API).
- **AppHost wiring:** `builder.AddKeycloak("keycloak", 8080)` with `.WithDataVolume()` and `.WithRealmImport("./KeycloakConfiguration")`. Port 8080 is pinned for stable OIDC cookie behavior.
- **Realm import:** `WarpBusiness.AppHost/KeycloakConfiguration/warpbusiness-realm.json` — realm `warpbusiness`, clients `warpbusiness-web` (public/OIDC) and `warpbusiness-api` (bearer-only).
- **API auth:** `AddKeycloakJwtBearer("keycloak", realm: "warpbusiness")` with audience `warpbusiness-api`. Weatherforecast endpoint protected with `.RequireAuthorization()`.
- **Keycloak reference** is passed to both API and Web projects in AppHost.cs.
