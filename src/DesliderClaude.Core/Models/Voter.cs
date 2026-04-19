namespace DesliderClaude.Core.Models;

public class Voter
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GameNightId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string VoterToken { get; set; } = string.Empty;

    /// <summary>Set when the voter is a signed-in user. Nullable so anonymous
    /// voters still work (CLAUDE.md: no account required, just a display name).</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Set when the voter is an anonymous visitor with a persistent
    /// cross-night cookie identity. Either this or <see cref="UserId"/> is set
    /// (but both can be null for legacy pre-Visitor anonymous voters).</summary>
    public Guid? VisitorId { get; set; }
    public Visitor? Visitor { get; set; }

    public GameNight GameNight { get; set; } = null!;
    public ICollection<Swipe> Swipes { get; set; } = new List<Swipe>();
}
