using DesliderClaude.Core.Services;
using DesliderClaude.Core.Services.Imps;
using DesliderClaude.Data.Services.Imps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DesliderClaude.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesliderData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DesliderClaudeDbContext>(opt => opt.UseSqlite(connectionString));
        services.AddSingleton<IShareCodeGenerator, HaikunatorShareCodeGenerator>();
        services.AddScoped<IGameNightService, GameNightService>();
        services.AddScoped<IVotingService, VotingService>();
        services.AddHealthChecks()
            .AddDbContextCheck<DesliderClaudeDbContext>("sqlite");
        return services;
    }
}
