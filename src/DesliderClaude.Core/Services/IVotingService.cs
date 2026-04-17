using DesliderClaude.Core.Entities;

namespace DesliderClaude.Core.Services;

public interface IVotingService
{
    Task<Voter> JoinAsync(Guid gameNightId, string displayName, string voterToken, CancellationToken ct = default);
    Task RecordSwipeAsync(string voterToken, Guid gameId, bool yes, CancellationToken ct = default);
    Task<IReadOnlyList<GameRanking>> GetRankingAsync(Guid gameNightId, CancellationToken ct = default);
    Task<VoterProgress?> GetProgressAsync(string voterToken, CancellationToken ct = default);
}
