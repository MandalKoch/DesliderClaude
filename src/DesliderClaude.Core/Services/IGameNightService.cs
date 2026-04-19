using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

public interface IGameNightService
{
    Task<GameNight> CreateAsync(
        string name,
        DateOnly? targetDate,
        IEnumerable<string> gameNames,
        Guid? createdByUserId = null,
        CancellationToken ct = default);

    Task<GameNight?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default);
    Task CloseAsync(Guid gameNightId, string hostToken, CancellationToken ct = default);

    /// <summary>Summary of every night the given user is part of (as host or voter).</summary>
    Task<IReadOnlyList<UserNightSummary>> ListForUserAsync(Guid userId, CancellationToken ct = default);
}
