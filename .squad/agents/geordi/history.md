# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** .NET Aspire application — web frontend, middle tier API, and PostgreSQL database
- **Stack:** .NET, Aspire, ASP.NET Core, PostgreSQL, Blazor
- **Created:** 2026-04-11

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- OIDC auth configured in `WarpBusiness.Web/Program.cs` — cookie + OpenIdConnect scheme against Keycloak realm `warpbusiness`, client `warpbusiness-web`
- Keycloak URL resolved from Aspire service discovery config: `services:keycloak:https:0` or `services:keycloak:http:0`
- Login/logout are minimal API endpoints (`/login`, `/logout`), not Razor pages
- `AuthorizeView` used in both `Home.razor` and `MainLayout.razor` (top-row) for auth state display
- `CascadingAuthenticationState` registered via DI (`AddCascadingAuthenticationState()`) — modern Blazor approach, no wrapper component needed
- No official Aspire Keycloak client auth NuGet exists; used standard `Microsoft.AspNetCore.Authentication.OpenIdConnect` v10.0.5
- `preferred_username` is the Keycloak claim for display name — set via `TokenValidationParameters.NameClaimType`
