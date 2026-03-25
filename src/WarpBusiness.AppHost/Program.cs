var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var db = postgres.AddDatabase("warpbusiness");

// Keycloak for local dev — only active when AuthProvider:ActiveProvider = Keycloak
var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    .WithDataVolume();

var api = builder.AddProject<Projects.WarpBusiness_Api>("api")
    .WithReference(db)
    .WithReference(keycloak)
    .WaitFor(db);

builder.AddProject<Projects.WarpBusiness_Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
