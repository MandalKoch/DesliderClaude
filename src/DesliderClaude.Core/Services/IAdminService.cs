using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

/// <summary>
/// Site-wide admin operations. Guarded by the <c>Admin</c> role at the page level.
/// </summary>
public interface IAdminService
{
    Task<AdminOverview> GetOverviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminNightRow>> ListNightsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GamePopularityRow>> TopGamesAsync(int take = 10, CancellationToken ct = default);

    /// <summary>Hard-delete a user. Their hosted nights and voter records lose the
    /// FK (SetNull), not the rows themselves.</summary>
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);

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
