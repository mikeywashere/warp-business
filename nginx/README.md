# nginx Reverse Proxy

This directory contains the nginx configuration template for subdomain-based routing in WarpBusiness.

## Subdomain Routing Table

| Subdomain | Service | Notes |
|-----------|---------|-------|
| `warp-business.com` | marketing-site | Public marketing (apex domain) |
| `www.warp-business.com` | marketing-site | www alias |
| `app.warp-business.com` | web | Main Blazor app frontend |
| `api.warp-business.com` | api | REST API |
| `portal.warp-business.com` | tenant-portal | Tenant management portal |
| `customer.warp-business.com` | customer-portal | Customer portal |
| `grafana.warp-business.com` | grafana:3000 | Observability (Grafana LGTM) |

## Template and envsubst

`nginx.conf.template` uses `${VAR}` syntax. The official `nginx:alpine` Docker image automatically runs `envsubst` on all files in `/etc/nginx/templates/` at startup, writing the rendered result to `/etc/nginx/conf.d/`.

### Environment Variables

| Variable | Example (dev) | Points to |
|----------|--------------|-----------|
| `MARKETING_UPSTREAM` | `http://localhost:5002` | marketing-site service |
| `WEB_UPSTREAM` | `http://localhost:5001` | web service |
| `API_UPSTREAM` | `http://localhost:5000` | api service |
| `TENANT_PORTAL_UPSTREAM` | `http://localhost:5003` | tenant-portal service |
| `CUSTOMER_PORTAL_UPSTREAM` | `http://localhost:5004` | customer-portal service |
| `GRAFANA_UPSTREAM` | `http://localhost:3000` | grafana container |

## Local Dev (Aspire)

In Aspire dev, upstream URLs are dynamic. `AppHost.cs` uses `GetEndpoint("http")` on each service resource and passes the result as `WithEnvironment(...)`. Aspire resolves these to the actual `http://localhost:<port>` values at startup.

## Production

In production, set the environment variables to your actual upstream addresses (internal Docker/k8s service names or IPs). Example `docker-compose` override:

```yaml
environment:
  MARKETING_UPSTREAM: http://marketing-site:5000
  WEB_UPSTREAM: http://web:5000
  API_UPSTREAM: http://api:5000
  TENANT_PORTAL_UPSTREAM: http://tenant-portal:5000
  CUSTOMER_PORTAL_UPSTREAM: http://customer-portal:5000
  GRAFANA_UPSTREAM: http://grafana:3000
```

## Health Checks

Every server block exposes `/_health` returning HTTP 200. Use this for load balancer or uptime monitoring health probes.
