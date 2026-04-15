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

var api = builder.AddProject<Projects.WarpBusiness_Api>("api")
    .WithExternalHttpEndpoints()
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(minio)
    .WaitFor(minio)
    .WithEnvironment("Keycloak__AdminUser", "admin")
    .WithEnvironment("Keycloak__AdminPassword", keycloak.Resource.AdminPasswordParameter);

builder.AddProject<Projects.WarpBusiness_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak);

builder.AddProject<Projects.WarpBusiness_CustomerPortal>("customer-portal")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak);

builder.AddProject<Projects.WarpBusiness_TenantPortal>("tenant-portal")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak);

builder.AddProject<Projects.WarpBusiness_MarketingSite>("marketing-site")
    .WithExternalHttpEndpoints();

builder.Build().Run();
