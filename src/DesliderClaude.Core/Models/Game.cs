namespace DesliderClaude.Core.Models;

public class Game
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GameNightId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>When set, this game originated from a BGG import. The FK points
    /// at the shared <see cref="BggGame"/> cache so we can follow back for any
    /// field we haven't denormalised here.</summary>
    public int? BggGameId { get; set; }
    public BggGame? BggGame { get; set; }

    /// <summary>Denormalised from BggGame for fast swipe-card render —
    /// avoids a join on every /swipe hit.</summary>
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }

    public GameNight GameNight { get; set; } = null!;
    public ICollection<Swipe> Swipes { get; set; } = new List<Swipe>();
}
