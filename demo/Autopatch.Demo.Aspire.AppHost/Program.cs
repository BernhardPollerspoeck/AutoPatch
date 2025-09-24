var builder = DistributedApplication.CreateBuilder(args);

// Add the AutoPatch demo server with service discovery
var autoPatchServer = builder.AddProject<Projects.Autopatch_Demo_Server>("autopatch")
    .WithHttpEndpoint(port: 5249, name: "http");

// Add the AutoPatch client that discovers the server
var autoPatchClient = builder.AddProject<Projects.Autopatch_Demo_Aspire_Client>("autopatch-client")
    .WithReference(autoPatchServer);

builder.Build().Run();