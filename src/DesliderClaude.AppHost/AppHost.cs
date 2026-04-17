var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.DesliderClaude_Web>("web");

builder.Build().Run();
