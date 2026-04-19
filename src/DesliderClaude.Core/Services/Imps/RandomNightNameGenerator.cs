namespace DesliderClaude.Core.Services.Imps;

public sealed class RandomNightNameGenerator : INightNameGenerator
{
    // Deliberately short, tonally consistent lists — easier to tune than a sprawling set.
    private static readonly string[] Adjectives =
    {
        "Reckless", "Tactical", "Cursed", "Midnight", "Legendary",
        "Greasy", "Humble", "Mighty", "Unhinged", "Pixelated",
        "Dubious", "Caffeinated", "Feral", "Mischievous", "Grizzled",
        "Regal", "Sneaky", "Valiant", "Cosmic", "Ruthless",
    };

    private static readonly string[] Nouns =
    {
        "Gambit", "Rally", "Showdown", "Escapade", "Caper",
        "Heist", "Throwdown", "Council", "Parley", "Odyssey",
        "Ambush", "Fiesta", "Skirmish", "Bonanza", "Summit",
        "Brawl", "Convergence", "Quest", "Saga", "Hullabaloo",
    };

    public string Generate()
    {
        var adj = Adjectives[Random.Shared.Next(Adjectives.Length)];
        var noun = Nouns[Random.Shared.Next(Nouns.Length)];
        return $"{adj} {noun}";
    }
}
