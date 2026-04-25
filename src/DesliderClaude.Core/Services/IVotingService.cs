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

    /// <summary>Per-voter progress for the host dashboard. Includes voter tokens.</summary>
    Task<IReadOnlyList<VoterStatus>> ListVoterStatusesAsync(Guid gameNightId, CancellationToken ct = default);

    /// <summary>Public per-voter snapshot for everyone-with-the-link surfaces.
    /// Omits the voter token.</summary>
    Task<IReadOnlyList<VoterPublicStatus>> ListPublicVoterStatusesAsync(Guid gameNightId, CancellationToken ct = default);

    /// <summary>Create a Voter pre-named by the host. Returns the voter so the
    /// host can hand the resulting <see cref="Voter.VoterToken"/> to the named
    /// person as a per-voter invite link. Caller is responsible for checking
    /// the requester actually owns the night.</summary>
    Task<Voter> CreateInviteAsync(Guid gameNightId, string displayName, CancellationToken ct = default);

    /// <summary>Remove a voter and (cascade) all their swipes from a night.
    /// No-op if the voter doesn't exist or doesn't belong to this night.
    /// Caller checks host ownership.</summary>
    Task RemoveVoterAsync(Guid voterId, Guid gameNightId, CancellationToken ct = default);

    /// <summary>
    /// Picks the next unvoted game for the voter. Returns <c>null</c> when every
    /// candidate has been swiped (the swipe flow is done; the voter must go to
    /// the dedicated manage-votes page to change or remove existing votes).
    /// </summary>
    Task<Game?> PickNextGameAsync(string voterToken, Guid? excludeGameId = null, CancellationToken ct = default);
}
