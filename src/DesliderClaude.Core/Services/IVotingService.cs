using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

public interface IVotingService
{
    Task<Voter> JoinAsync(
        Guid gameNightId,
        string displayName,
        string voterToken,
        Guid? userId = null,
        Guid? visitorId = null,
        CancellationToken ct = default);
    Task RecordSwipeAsync(string voterToken, Guid gameId, bool yes, CancellationToken ct = default);
    Task<IReadOnlyList<GameRanking>> GetRankingAsync(Guid gameNightId, CancellationToken ct = default);
    Task<int> GetVoterCountAsync(Guid gameNightId, CancellationToken ct = default);
    Task<VoterProgress?> GetProgressAsync(string voterToken, CancellationToken ct = default);

    /// <summary>
    /// Picks the next game for the voter using weighted random sampling.
    /// Unvoted games are strongly preferred; voted games stay in the pool with weight
    /// growing as time passes since the last swipe (never zero). The game just swiped
    /// (<paramref name="excludeGameId"/>) is avoided unless it is the only option.
    /// </summary>
    Task<Game?> PickNextGameAsync(string voterToken, Guid? excludeGameId = null, CancellationToken ct = default);
}
