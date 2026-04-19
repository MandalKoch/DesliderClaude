namespace DesliderClaude.Core.Models;

/// <summary>
/// A night the user is part of — projection shape used by the home-page list.
/// </summary>
/// <param name="ShareCode">For building URLs.</param>
/// <param name="Name">Display name.</param>
/// <param name="TargetDate">Host-set target date, if any.</param>
/// <param name="IsClosed">True once the host closed voting.</param>
/// <param name="ClosedAt">When the night was closed.</param>
/// <param name="IsHost">True if the user created this night.</param>
/// <param name="IsVoter">True if the user has joined as voter.</param>
/// <param name="GameCount">Total candidate games on this night.</param>
/// <param name="SwipesByViewer">Distinct games the viewer has swiped. Always &lt;= <paramref name="GameCount"/>.</param>
public sealed record UserNightSummary(
    string ShareCode,
    string Name,
    DateOnly? TargetDate,
    bool IsClosed,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    bool IsHost,
    bool IsVoter,
    int GameCount,
    int SwipesByViewer)
{
    public bool HasMissingVote => IsVoter && !IsClosed && SwipesByViewer < GameCount;
}
