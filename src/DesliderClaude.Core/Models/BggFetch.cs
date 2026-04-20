namespace DesliderClaude.Core.Models;

/// <summary>DTOs returned by <see cref="Services.IBggClient"/>. Shape matches
/// what BGG's XML API actually gives us, not our persisted entities.</summary>
public sealed record BggGeekListFetch(int GeekListId, string Name, IReadOnlyList<int> GameIds);

public sealed record BggCollectionFetch(string Username, IReadOnlyList<int> GameIds);

public sealed record BggThingFetch(
    int BggGameId,
    string Name,
    string? ImageUrl,
    string? ThumbnailUrl,
    int? MinPlayers,
    int? MaxPlayers,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    IReadOnlyList<BggPlayerCountVote> RecommendedPlayerCounts);
