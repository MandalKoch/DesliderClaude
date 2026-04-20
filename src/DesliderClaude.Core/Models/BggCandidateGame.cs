namespace DesliderClaude.Core.Models;

/// <summary>A game from a user's selected BGG imports, with enough metadata
/// for the /create page to apply filters client-side. Deduped by BggGameId
/// across imports.</summary>
public sealed record BggCandidateGame(
    int BggGameId,
    string Name,
    string? ImageUrl,
    string? ThumbnailUrl,
    int? MinPlayers,
    int? MaxPlayers,
    int? MinPlayTimeMinutes,
    int? MaxPlayTimeMinutes,
    IReadOnlyList<BggPlayerCountVote> RecommendedPlayerCounts);
