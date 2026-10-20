var builder = DistributedApplication.CreateBuilder(args);

// Data volumes keep the data between runs; without them every restart starts empty.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var lojaDb = postgres.AddDatabase("lojadb");

var redis = builder.AddRedis("redis")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

builder.Build().Run();
