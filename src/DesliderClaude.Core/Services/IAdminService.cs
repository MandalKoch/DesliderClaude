using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

/// <summary>
/// Site-wide admin operations. Guarded by the configured HTTP Basic Auth middleware on <c>/admin</c> routes.
/// </summary>
public interface IAdminService
{
    Task<AdminOverview> GetOverviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminNightRow>> ListNightsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminGatheringRow>> ListGatheringsAsync(CancellationToken ct = default);
    Task<AdminGatheringDetail?> GetGatheringDetailAsync(string shareCode, CancellationToken ct = default);
    Task<IReadOnlyList<GamePopularityRow>> TopGamesAsync(int take = 10, CancellationToken ct = default);

    /// <summary>Hard-delete a user. Their hosted nights and voter records lose the
    /// FK (SetNull), not the rows themselves.</summary>
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Admin-override password reset. Sets (or overwrites) the local
    /// password hash; doesn't require the user's current password.</summary>
    Task SetUserPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);

    /// <summary>Hard-delete a Game Night, cascading games / voters / swipes.</summary>
    Task DeleteNightAsync(Guid nightId, CancellationToken ct = default);

    /// <summary>Close voting on a Game Night without needing the host token. No-op
    /// if the night is already closed.</summary>
    Task CloseNightAsync(Guid nightId, CancellationToken ct = default);

    /// <summary>Re-open a closed Game Night. No-op if the night is already open.</summary>
    Task ReopenNightAsync(Guid nightId, CancellationToken ct = default);

    /// <summary>List voters on a specific night with per-voter activity.</summary>
    Task<IReadOnlyList<AdminVoterRow>> ListVotersForNightAsync(Guid nightId, CancellationToken ct = default);

    /// <summary>Hard-delete a voter (cascades their swipes).</summary>
    Task DeleteVoterAsync(Guid voterId, CancellationToken ct = default);
}
