namespace DesliderClaude.Core.Models;

public sealed record AdminOverview(
    int UserCount,
    int NightCount,
    int OpenNightCount,
    int ClosedNightCount,
    int VoterCount,
    int SwipeCount,
    int SwipesLast24h);

public sealed record AdminUserRow(
    Guid Id,
    string Username,
    DateTimeOffset CreatedAt,
    bool HasPassword,
    IReadOnlyList<string> ExternalProviders,
    int NightsHosted,
    int NightsVotedIn,
    int SwipeCount);

public sealed record AdminNightRow(
    Guid Id,
    string ShareCode,
    string Name,
    DateOnly? TargetDate,
    bool IsClosed,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    string? HostUsername,
    int GameCount,
    int VoterCount,
    int SwipeCount);

public sealed record GamePopularityRow(string Name, int YesCount, int NoCount)
{
    public int TotalVotes => YesCount + NoCount;
}

public sealed record AdminVoterRow(
    Guid Id,
    string DisplayName,
    string? Username,
    int SwipeCount,
    int YesCount,
    int NoCount);
