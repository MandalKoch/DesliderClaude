using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

public interface IGatheringService
{
    Task<GatheringView> CreateAsync(string name, Guid hostUserId, CancellationToken ct = default);
    Task<GatheringView?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default);
    Task<IReadOnlyList<GatheringSummary>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task<GatheringMemberView?> GetMemberAsync(Guid gatheringId, string memberToken, CancellationToken ct = default);
    Task<GatheringMemberView> JoinAsync(Guid gatheringId, string displayName, string memberToken, Guid? userId = null, CancellationToken ct = default);
    Task AddGameAsync(Guid gatheringId, string memberToken, GameCandidate candidate, CancellationToken ct = default);
    Task RemoveGameAsync(Guid gatheringId, string memberToken, Guid gameId, Guid? requestingUserId = null, CancellationToken ct = default);
    Task SetInterestAsync(Guid gatheringId, string memberToken, Guid gameId, bool interested, CancellationToken ct = default);
    Task ConfirmGameAsync(Guid gatheringId, Guid hostUserId, Guid gameId, bool isConfirmed, Guid? bringerMemberId, CancellationToken ct = default);
    Task<GameNight> StartVoteAsync(Guid gatheringId, Guid hostUserId, string name, IReadOnlyList<Guid> gameIds, CancellationToken ct = default);
    Task MarkPlayedAsync(Guid gatheringId, Guid hostUserId, Guid gameId, CancellationToken ct = default);
}
