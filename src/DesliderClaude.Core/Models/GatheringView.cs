namespace DesliderClaude.Core.Models;

public sealed record GatheringView(
    Guid Id, string Name, string ShareCode, Guid? CreatedByUserId,
    IReadOnlyList<GatheringMemberView> Members,
    IReadOnlyList<GatheringGameView> Games,
    IReadOnlyList<GatheringRoundView> Rounds,
    IReadOnlyList<GatheringPlayedGameView> PlayedGames,
    bool IsVotingClosed = false);

public sealed record GatheringMemberView(Guid Id, string DisplayName, Guid? UserId);

public sealed record GatheringGameView(
    Guid Id, string Name, int? BggGameId, string? ImageUrl, string? ThumbnailUrl,
    Guid SuggestedByMemberId, bool IsConfirmed, Guid? BringerMemberId,
    IReadOnlyList<GatheringInterestView> Interests, bool IsUsedInVote = false);

public sealed record GatheringInterestView(Guid MemberId, bool Interested);
public sealed record GatheringRoundView(Guid Id, string Name, string ShareCode, bool IsClosed, DateTimeOffset CreatedAt);
public sealed record GatheringPlayedGameView(Guid Id, Guid GatheringGameId, string GameName, DateTimeOffset PlayedAt);
public sealed record GatheringSummary(Guid Id, string Name, string ShareCode, int GameCount, int MemberCount, bool IsHost);
