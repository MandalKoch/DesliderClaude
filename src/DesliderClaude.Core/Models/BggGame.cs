namespace DesliderClaude.Core.Models;

/// <summary>Cached BGG "thing" record, shared across users. Keyed by BGG's numeric ID.
/// RecommendedPlayerCountsJson is a serialised IReadOnlyList&lt;BggPlayerCountVote&gt;.</summary>
public class BggGame
{
    public int BggGameId { get; set; }

    /// <summary>BGG item type: "boardgame", "boardgameexpansion", or "boardgameaccessory".
    /// Null on rows cached before the column existed — refreshed lazily on next fetch.</summary>
    public string? Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int? MinPlayTimeMinutes { get; set; }
    public int? MaxPlayTimeMinutes { get; set; }

    /// <summary>JSON of <see cref="BggPlayerCountVote"/>[] — parsed from BGG's
    /// suggested_numplayers poll. Null when the poll had no data.</summary>
    public string? RecommendedPlayerCountsJson { get; set; }

    public DateTimeOffset LastFetchedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<BggImportItem> ImportItems { get; set; } = new List<BggImportItem>();
}

public enum BggPlayerCountKind
{
    NotRecommended = 0,
    Recommended = 1,
    Best = 2,
}

public sealed record BggPlayerCountVote(int Count, BggPlayerCountKind Kind);
