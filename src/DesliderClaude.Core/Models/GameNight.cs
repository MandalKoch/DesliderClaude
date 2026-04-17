namespace DesliderClaude.Core.Models;

public class GameNight
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public DateOnly? TargetDate { get; set; }
    public string ShareCode { get; set; } = string.Empty;
    public string HostToken { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<Game> Games { get; set; } = new List<Game>();
    public ICollection<Voter> Voters { get; set; } = new List<Voter>();
}
