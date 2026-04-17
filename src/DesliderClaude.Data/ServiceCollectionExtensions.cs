using DesliderClaude.Core.Services;
using DesliderClaude.Data.Services;
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
        return services;
    }
}
