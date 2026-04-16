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

builder.AddProject<Projects.WarpBusiness_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak)
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

builder.AddProject<Projects.WarpBusiness_CustomerPortal>("customer-portal")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak)
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

builder.AddProject<Projects.WarpBusiness_TenantPortal>("tenant-portal")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak)
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

builder.AddProject<Projects.WarpBusiness_MarketingSite>("marketing-site")
    .WithExternalHttpEndpoints()
    .WithEnvironment("GRAFANA_OTLP_ENDPOINT", grafana.GetEndpoint("otlp-grpc"));

builder.Build().Run();
