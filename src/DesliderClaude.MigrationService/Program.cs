using DesliderClaude.Data;
using DesliderClaude.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDesliderData(
    builder.Configuration.GetConnectionString("DesliderClaudeDb")
    ?? throw new InvalidOperationException("Missing connection string 'DesliderClaudeDb'."));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DesliderClaudeDbContext>();
    await db.Database.MigrateAsync();
}

app.MapDefaultEndpoints();

app.MapPost("/seed", async (DesliderClaudeDbContext db, CancellationToken ct) =>
{
    await SeedData.ResetAndSeedAsync(db, ct);
    return Results.Ok(new { message = "Database reset and seeded." });
});

app.Run();
