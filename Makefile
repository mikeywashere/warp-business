.PHONY: build load-kind load-minikube deploy undeploy status logs-api logs-web logs-portal restart clean

CLUSTER ?= docker-desktop
NAMESPACE = warp-business
IMG_API = warp-business/api:latest
IMG_WEB = warp-business/web:latest
IMG_PORTAL = warp-business/portal:latest

## Build all Docker images (run from repo root, context is src/)
build:
	docker build -t $(IMG_API)    -f src/WarpBusiness.Api/Dockerfile           src/
	docker build -t $(IMG_WEB)    -f src/WarpBusiness.Web/Dockerfile           src/
	docker build -t $(IMG_PORTAL) -f src/WarpBusiness.CustomerPortal/Dockerfile src/

## Load images into kind cluster
load-kind:
	kind load docker-image $(IMG_API)    --name warp-business
	kind load docker-image $(IMG_WEB)    --name warp-business
	kind load docker-image $(IMG_PORTAL) --name warp-business

## Point shell to minikube Docker daemon, then build
build-minikube:
	@echo "Run: eval \$$(minikube docker-env) && make build"

## Deploy to local K8s cluster (ensure k8s/secrets.yaml exists first)
deploy:
	@test -f k8s/secrets.yaml || (echo "ERROR: k8s/secrets.yaml not found. Copy from k8s/secrets.yaml.template and fill in values." && exit 1)
	kubectl apply -k k8s/

## Tear down all resources
undeploy:
	kubectl delete -k k8s/ --ignore-not-found

## Show pod status
status:
	kubectl get pods -n $(NAMESPACE)
	kubectl get services -n $(NAMESPACE)
	kubectl get ingress -n $(NAMESPACE)

## Tail API logs
logs-api:
	kubectl logs -n $(NAMESPACE) -l app=warp-api -f

## Tail Web logs
logs-web:
	kubectl logs -n $(NAMESPACE) -l app=warp-web -f

## Tail Portal logs
logs-portal:
	kubectl logs -n $(NAMESPACE) -l app=warp-portal -f

## Rolling restart all deployments
restart:
	kubectl rollout restart deployment -n $(NAMESPACE)

## Delete namespace (full teardown including PVCs)
clean:
	kubectl delete namespace $(NAMESPACE) --ignore-not-found
