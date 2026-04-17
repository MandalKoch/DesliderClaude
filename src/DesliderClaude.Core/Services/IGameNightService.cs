using DesliderClaude.Core.Entities;

namespace DesliderClaude.Core.Services;

public interface IGameNightService
{
    Task<GameNight> CreateAsync(string name, DateOnly? targetDate, IEnumerable<string> gameNames, CancellationToken ct = default);
    Task<GameNight?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default);
    Task CloseAsync(Guid gameNightId, string hostToken, CancellationToken ct = default);
}
