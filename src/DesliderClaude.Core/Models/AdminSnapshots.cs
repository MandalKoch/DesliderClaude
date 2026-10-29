namespace DesliderClaude.Core.Models;

public sealed record AdminOverview(
    int UserCount,
    int NightCount,
    int OpenNightCount,
    int ClosedNightCount,
    int VoterCount,
    int SwipeCount,
    int SwipesLast24h,
    int GatheringCount = 0,
    int OpenGatheringCount = 0,
    int ClosedGatheringCount = 0,
    int GatheringMemberCount = 0,
    int GatheringGameCount = 0,
    int GatheringVoteCount = 0);

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
    int NoCount,
    IReadOnlyList<AdminGameChoice> Choices);

public sealed record AdminGameChoice(Guid GameId, string GameName, bool? Yes);

public sealed record AdminGatheringRow(
    Guid Id,
    string ShareCode,
    string Name,
    string? HostUsername,
    bool IsVotingClosed,
    DateTimeOffset CreatedAt,
    int MemberCount,
    int GameCount,
    int VoteCount);

public sealed record AdminGatheringGame(Guid Id, string Name, string? BringerDisplayName);

public sealed record AdminGatheringMember(
    Guid Id,
    string DisplayName,
    string? Username,
    IReadOnlyList<AdminGameChoice> Choices);

public sealed record AdminGatheringDetail(
    AdminGatheringRow Gathering,
    IReadOnlyList<AdminGatheringGame> Games,
    IReadOnlyList<AdminGatheringMember> Participants,
    IReadOnlyList<GatheringRoundView> Rounds,
    IReadOnlyList<GatheringPlayedGameView> PlayedGames);
