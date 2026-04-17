namespace DesliderClaude.Core.Models;

public class Game
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GameNightId { get; set; }
    public string Name { get; set; } = string.Empty;

    public GameNight GameNight { get; set; } = null!;
    public ICollection<Swipe> Swipes { get; set; } = new List<Swipe>();
}
