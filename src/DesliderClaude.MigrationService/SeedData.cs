using DesliderClaude.Core.Entities;
using DesliderClaude.Data;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.MigrationService;

internal static class SeedData
{
    private const string SampleShareCode = "sample-night";

    public static async Task ResetAndSeedAsync(DesliderClaudeDbContext db, CancellationToken ct)
    {
        db.Swipes.RemoveRange(db.Swipes);
        db.Voters.RemoveRange(db.Voters);
        db.Games.RemoveRange(db.Games);
        db.GameNights.RemoveRange(db.GameNights);
        await db.SaveChangesAsync(ct);

        var night = new GameNight
        {
            Name = "Friday Sample Night",
            TargetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            ShareCode = SampleShareCode,
            HostToken = "sample-host-token",
        };

        var catan = new Game { Name = "Catan" };
        var azul = new Game { Name = "Azul" };
        var wingspan = new Game { Name = "Wingspan" };
        var terraforming = new Game { Name = "Terraforming Mars" };
        var sevenWonders = new Game { Name = "7 Wonders" };
        var codenames = new Game { Name = "Codenames" };
        night.Games = new List<Game> { catan, azul, wingspan, terraforming, sevenWonders, codenames };

        var alice = new Voter { GameNightId = night.Id, DisplayName = "Alice", VoterToken = "sample-voter-alice" };
        var bob = new Voter { GameNightId = night.Id, DisplayName = "Bob", VoterToken = "sample-voter-bob" };
        var cara = new Voter { GameNightId = night.Id, DisplayName = "Cara", VoterToken = "sample-voter-cara" };
        night.Voters = new List<Voter> { alice, bob, cara };

        db.GameNights.Add(night);

        db.Swipes.AddRange(
            new Swipe { VoterId = alice.Id, GameId = catan.Id, Yes = true },
            new Swipe { VoterId = alice.Id, GameId = azul.Id, Yes = true },
            new Swipe { VoterId = alice.Id, GameId = wingspan.Id, Yes = false },
            new Swipe { VoterId = bob.Id, GameId = catan.Id, Yes = true },
            new Swipe { VoterId = bob.Id, GameId = terraforming.Id, Yes = true },
            new Swipe { VoterId = bob.Id, GameId = codenames.Id, Yes = false },
            new Swipe { VoterId = cara.Id, GameId = azul.Id, Yes = true },
            new Swipe { VoterId = cara.Id, GameId = wingspan.Id, Yes = true },
            new Swipe { VoterId = cara.Id, GameId = sevenWonders.Id, Yes = true });

        await db.SaveChangesAsync(ct);
    }
}
