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

    /// <summary>Close voting. Throws <see cref="UnauthorizedAccessException"/> if
    /// the caller isn't the night's creator.</summary>
    Task CloseAsync(Guid gameNightId, Guid requestingUserId, CancellationToken ct = default);

    /// <summary>Re-open a previously closed night. Creator-only; admin has its
    /// own bypass in <see cref="IAdminService"/>.</summary>
    Task ReopenAsync(Guid gameNightId, Guid requestingUserId, CancellationToken ct = default);

    /// <summary>Summary of every night the given user is part of (as host or voter).</summary>
    Task<IReadOnlyList<UserNightSummary>> ListForUserAsync(Guid userId, CancellationToken ct = default);
}
