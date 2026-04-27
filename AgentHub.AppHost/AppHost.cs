var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AgentHub_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.AgentHub_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
