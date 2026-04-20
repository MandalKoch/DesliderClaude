using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using DesliderClaude.Core.Services.Imps;
using DesliderClaude.Data.Services.Imps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace DesliderClaude.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesliderData(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null)
    {
        services.AddDbContext<DesliderClaudeDbContext>(opt => opt.UseSqlite(connectionString));
        services.AddSingleton<IShareCodeGenerator, HaikunatorShareCodeGenerator>();
        services.AddSingleton<INightNameGenerator, RandomNightNameGenerator>();
        services.AddScoped<IGameNightService, GameNightService>();
        services.AddScoped<IVotingService, VotingService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IBggImportService, BggImportService>();

        if (configuration is not null)
        {
            services.Configure<BggOptions>(configuration.GetSection(BggOptions.Section));
        }
        else
        {
            services.AddOptions<BggOptions>();
        }

        // BGG XML API client. IHttpClientFactory handles handler pooling; the
        // standard resilience pipeline layers retry (3 attempts, exponential
        // backoff with jitter) + circuit breaker + attempt/total timeouts.
        // Since BGG locked the XML API behind Bearer auth (Oct 2025), we pull
        // the token from BggOptions at client-creation time.
        services.AddHttpClient<IBggClient, BggClient>((sp, http) =>
        {
            http.BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2/");
            http.Timeout = TimeSpan.FromSeconds(45);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DesliderClaude/1.0 (+https://desliderclaude.fly.dev)");

            var token = sp.GetRequiredService<IOptions<BggOptions>>().Value.ApiToken;
            if (!string.IsNullOrWhiteSpace(token))
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Trim());
            }
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
