namespace DesliderClaude.Core.Models;

public class Swipe
{
    public Guid VoterId { get; set; }
    public Guid GameId { get; set; }
    public bool Yes { get; set; }
    public DateTimeOffset SwipedAt { get; set; } = DateTimeOffset.UtcNow;

    public Voter Voter { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
