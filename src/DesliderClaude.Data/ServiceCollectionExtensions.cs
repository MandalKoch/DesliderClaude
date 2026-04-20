using DesliderClaude.Core.Services;
using DesliderClaude.Core.Services.Imps;
using DesliderClaude.Data.Services.Imps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace DesliderClaude.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesliderData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DesliderClaudeDbContext>(opt => opt.UseSqlite(connectionString));
        services.AddSingleton<IShareCodeGenerator, HaikunatorShareCodeGenerator>();
        services.AddSingleton<INightNameGenerator, RandomNightNameGenerator>();
        services.AddScoped<IGameNightService, GameNightService>();
        services.AddScoped<IVotingService, VotingService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IBggImportService, BggImportService>();

        // BGG XML API client. IHttpClientFactory handles handler pooling; the
        // standard resilience pipeline layers retry (3 attempts, exponential
        // backoff with jitter) + circuit breaker + attempt/total timeouts —
        // matches our "3 max, increasing delay, break on repeated failure" rule.
        services.AddHttpClient<IBggClient, BggClient>(http =>
        {
            http.BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2/");
            http.Timeout = TimeSpan.FromSeconds(45);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DesliderClaude/1.0 (+https://desliderclaude.fly.dev)");
        }).AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = 3;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
            o.Retry.UseJitter = true;
        });
        services.AddHealthChecks()
            .AddDbContextCheck<DesliderClaudeDbContext>("sqlite");
        return services;
    }
}
