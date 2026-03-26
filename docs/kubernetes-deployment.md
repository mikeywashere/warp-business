# Kubernetes Deployment Guide

## Prerequisites
- Docker Desktop with Kubernetes enabled, OR minikube, OR kind
- kubectl configured to target your local cluster
- make (optional, for Makefile shortcuts)

## Quick Start

### 1. Enable local K8s
**Docker Desktop:** Settings → Kubernetes → Enable  
**minikube:** `minikube start --memory=4096 --cpus=2`  
**kind:** `kind create cluster --name warp-business`

### 2. Install nginx ingress controller
```bash
# Docker Desktop / kind
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.10.0/deploy/static/provider/cloud/deploy.yaml

# minikube
minikube addons enable ingress
```

### 3. Add local DNS entries
Add to `/etc/hosts` (Linux/Mac) or `C:\Windows\System32\drivers\etc\hosts` (Windows):
```
127.0.0.1  api.warp-business.local
127.0.0.1  app.warp-business.local
127.0.0.1  portal.warp-business.local
```

### 4. Copy and configure secrets
```bash
cp k8s/secrets.yaml.template k8s/secrets.yaml
# Edit k8s/secrets.yaml with your values (base64 encoded)
# NEVER commit secrets.yaml
```

### 5. Build and load images

**Docker Desktop / kind:**
```bash
make build          # builds all images
make load-kind      # loads into kind cluster (if using kind)
```

**minikube:**
```bash
eval $(minikube docker-env)
make build
```

### 6. Deploy
```bash
make deploy         # kubectl apply -k k8s/
make status         # check pod status
```

### 7. Verify
- CRM UI: http://app.warp-business.local
- Customer Portal: http://portal.warp-business.local
- API: http://api.warp-business.local/swagger

## Build Context

All Dockerfiles are built from `src/` as the Docker build context — this is required because
each Dockerfile copies sibling project directories. Example:

```bash
docker build -t warp-api:local -f src/WarpBusiness.Api/Dockerfile src/
docker build -t warp-web:local -f src/WarpBusiness.Web/Dockerfile src/
docker build -t warp-portal:local -f src/WarpBusiness.CustomerPortal/Dockerfile src/
```

## Ingress Routing
| Host | Service | Port |
|------|---------|------|
| api.warp-business.local | warp-api | 8080 |
| app.warp-business.local | warp-web | 8080 |
| portal.warp-business.local | warp-portal | 8080 |

## Plugin Directory
The API container has `/app/plugins` for third-party DLL plugins.
Mount a PersistentVolumeClaim to `/app/plugins` to add external plugins without rebuilding.

## Environment Variables (Key)
| Variable | Purpose | Example |
|----------|---------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL | `Host=warp-postgres;...` |
| `services__api__http__0` | API URL for Web/Portal | `http://warp-api:8080` |
| `Jwt__Key` | JWT signing key | 32+ char secret |
| `AuthProvider__ActiveProvider` | Auth provider | `Local` |

> **Aspire service discovery:** The `services__api__http__0` env var pattern is how Aspire
> injects service URLs at runtime. In K8s, set this directly in the deployment manifest to
> point at the Kubernetes Service name — no code changes required.

## Keycloak (optional)
Keycloak is deployed but not required. Set `AuthProvider__ActiveProvider=Keycloak` and configure
`AuthProvider__Keycloak__*` env vars to enable it.

## Updating / Redeploying
```bash
make build          # rebuild images
make deploy         # apply changes (K8s rolling update)
make logs-api       # tail API logs
make restart        # rollout restart all deployments
```
