namespace DesliderClaude.Core.Models;

public class Voter
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GameNightId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string VoterToken { get; set; } = string.Empty;

    public GameNight GameNight { get; set; } = null!;
    public ICollection<Swipe> Swipes { get; set; } = new List<Swipe>();
}
