# Changelog

All notable changes to Warp Business are documented here.

## [1.0.0] - 2026-03-26

### Added
- CRM plugin — Contacts, Companies, Deals, Activities with full CRUD
- Custom fields for contacts (user-defined key/value fields)
- Employee Management plugin — add/remove employees, soft-delete/hard-delete
- Authentication — ASP.NET Core Identity + JWT with refresh tokens
- Microsoft OIDC and Keycloak OIDC as alternative auth providers
- Admin UI for managing users and auth provider assignments
- Customer Portal — separate container for external customer self-service
- Plugin system — ICustomModule interface, DLL drop-folder auto-discovery
- .NET Aspire orchestration for local development
- Kubernetes deployment manifests with resource limits and liveness probes
- Security hardening — Admin-only delete endpoints, ownership enforcement, email normalization, DTO validation
- Integration test coverage for all major controllers
