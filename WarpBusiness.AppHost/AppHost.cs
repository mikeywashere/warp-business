var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("warpdb");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()
    .WithRealmImport("./KeycloakConfiguration");

var api = builder.AddProject<Projects.WarpBusiness_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithEnvironment("Keycloak__AdminUser", "admin");

builder.AddProject<Projects.WarpBusiness_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(keycloak);

builder.Build().Run();
