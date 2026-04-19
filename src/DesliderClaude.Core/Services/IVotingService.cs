using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

public interface IVotingService
{
    Task<Voter> JoinAsync(
        Guid gameNightId,
        string displayName,
        string voterToken,
        Guid? userId = null,
        CancellationToken ct = default);

    Task RecordSwipeAsync(string voterToken, Guid gameId, bool yes, CancellationToken ct = default);

    /// <summary>Delete a swipe the voter previously cast.</summary>
    Task RemoveSwipeAsync(string voterToken, Guid gameId, CancellationToken ct = default);

    Task<IReadOnlyList<GameRanking>> GetRankingAsync(Guid gameNightId, CancellationToken ct = default);
    Task<int> GetVoterCountAsync(Guid gameNightId, CancellationToken ct = default);
    Task<VoterProgress?> GetProgressAsync(string voterToken, CancellationToken ct = default);

    /// <summary>
    /// Picks the next unvoted game for the voter. Returns <c>null</c> when every
    /// candidate has been swiped (the swipe flow is done; the voter must go to
    /// the dedicated manage-votes page to change or remove existing votes).
    /// </summary>
    Task<Game?> PickNextGameAsync(string voterToken, Guid? excludeGameId = null, CancellationToken ct = default);
}
