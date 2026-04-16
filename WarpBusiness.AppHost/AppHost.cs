using CommunityToolkit.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("warpdb-data")
    .WithPgAdmin()
    .AddDatabase("warpdb");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()
    .WithRealmImport("./KeycloakConfiguration")
    .WithBindMount("../keycloak/themes/warp", "/opt/keycloak/themes/warp");

var minio = builder.AddMinioContainer("minio")
    .WithDataVolume("minio-data");

// LGTM observability stack (Loki, Grafana, Tempo, Mimir) — all-in-one dev image
// UI at :3000 (admin/admin), OTLP gRPC receiver at :4317
var grafana = builder.AddContainer("grafana", "grafana/otel-lgtm")
    .WithVolume("grafana-data", "/var/lib/grafana")
    .WithHttpEndpoint(targetPort: 3000, name: "grafana-ui")
    .WithHttpEndpoint(targetPort: 4317, name: "otlp-grpc")
    .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin");

var api = builder.AddProject<Projects.WarpBusiness_Api>("api")
    .WithExternalHttpEndpoints()
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(minio)
    .WaitFor(minio)
    .WithEnvironment("Keycloak__AdminUser", "admin")
    .WithEnvironment("Keycloak__AdminPassword", keycloak.Resource.AdminPasswordParameter)
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

var web = builder.AddProject<Projects.WarpBusiness_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak)
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

var customerPortal = builder.AddProject<Projects.WarpBusiness_CustomerPortal>("customer-portal")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak)
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

var tenantPortal = builder.AddProject<Projects.WarpBusiness_TenantPortal>("tenant-portal")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak)
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

var marketingSite = builder.AddProject<Projects.WarpBusiness_MarketingSite>("marketing-site")
    .WithExternalHttpEndpoints()
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

// nginx reverse proxy — subdomain-based routing for warp-business.com
// See nginx/README.md for the full routing table and production usage.
builder.AddContainer("nginx", "nginx", "alpine")
    .WithBindMount("../nginx/nginx.conf.template", "/tmp/nginx.conf.template", isReadOnly: true)
    .WithHttpEndpoint(targetPort: 80, name: "http")
    .WithEnvironment("MARKETING_UPSTREAM", marketingSite.GetEndpoint("http"))
    .WithEnvironment("WEB_UPSTREAM", web.GetEndpoint("http"))
    .WithEnvironment("API_UPSTREAM", api.GetEndpoint("http"))
    .WithEnvironment("TENANT_PORTAL_UPSTREAM", tenantPortal.GetEndpoint("http"))
    .WithEnvironment("CUSTOMER_PORTAL_UPSTREAM", customerPortal.GetEndpoint("http"))
    .WithEnvironment("GRAFANA_UPSTREAM", grafana.GetEndpoint("grafana-ui"))
    .WithEnvironment("KEYCLOAK_UPSTREAM", keycloak.GetEndpoint("http"))
    .WithEntrypoint("/bin/sh")
    .WithArgs("-c", "set -x && envsubst '$MARKETING_UPSTREAM $WEB_UPSTREAM $API_UPSTREAM $TENANT_PORTAL_UPSTREAM $CUSTOMER_PORTAL_UPSTREAM $GRAFANA_UPSTREAM $KEYCLOAK_UPSTREAM' < /tmp/nginx.conf.template > /etc/nginx/conf.d/default.conf && echo '=== GENERATED CONFIG ===' && cat /etc/nginx/conf.d/default.conf && nginx -t && nginx -g 'daemon off;'")
    .WaitFor(api)
    .WaitFor(web)
    .WaitFor(customerPortal)
    .WaitFor(tenantPortal)
    .WaitFor(marketingSite)
    .WaitFor(grafana);

builder.Build().Run();
