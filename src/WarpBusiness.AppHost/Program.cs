var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var db = postgres.AddDatabase("warpbusiness");

var api = builder.AddProject<Projects.WarpBusiness_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.AddProject<Projects.WarpBusiness_Web>("web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
