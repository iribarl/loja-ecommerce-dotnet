var builder = DistributedApplication.CreateBuilder(args);

// Data volumes keep the data between runs; without them every restart starts empty.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var lojaDb = postgres.AddDatabase("lojadb");

var redis = builder.AddRedis("redis")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// WithReference injects the connection string; WaitFor delays startup until the resource is healthy.
var api = builder.AddProject<Projects.Loja_Api>("api")
    .WithReference(lojaDb)
    .WithReference(redis)
    .WaitFor(lojaDb)
    .WaitFor(redis);

builder.AddProject<Projects.Loja_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(lojaDb)
    .WithReference(redis)
    .WaitFor(lojaDb)
    .WaitFor(redis)
    .WaitFor(api);

builder.Build().Run();
