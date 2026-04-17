var builder = DistributedApplication.CreateBuilder(args);

// Shared SQLite file — both services must see the same DB on disk.
// Parked under %TEMP%/desliderclaude so it persists across AppHost restarts but doesn't pollute the repo.
var dbFile = Path.Combine(Path.GetTempPath(), "desliderclaude", "desliderclaude.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbFile)!);
var dbConnectionString = $"Data Source={dbFile}";

var migrations = builder.AddProject<Projects.DesliderClaude_MigrationService>("migrations")
    .WithEnvironment("ConnectionStrings__DesliderClaudeDb", dbConnectionString);

migrations.WithCommand(
    name: "seed",
    displayName: "Reset & seed sample data",
    executeCommand: async ctx =>
    {
        try
        {
            var endpoint = migrations.GetEndpoint("http");
            using var http = new HttpClient { BaseAddress = new Uri(endpoint.Url) };
            var response = await http.PostAsync("/seed", content: null, ctx.CancellationToken);
            return response.IsSuccessStatusCode
                ? CommandResults.Success()
                : CommandResults.Failure($"Seed failed: HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return CommandResults.Failure(ex.Message);
        }
    },
    commandOptions: new CommandOptions
    {
        Description = "Drops all data and inserts a sample Game Night so you can poke around the UI.",
        ConfirmationMessage = "This wipes every row in the database and replaces it with sample data. Continue?",
        IconName = "DatabaseArrowDown",
    });

builder.AddProject<Projects.DesliderClaude_Web>("web")
    .WithEnvironment("ConnectionStrings__DesliderClaudeDb", dbConnectionString)
    .WaitFor(migrations);

builder.Build().Run();
