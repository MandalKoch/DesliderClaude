using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data.Services.Imps;

internal sealed class VotingService : IVotingService
{
    private readonly DesliderClaudeDbContext _db;

    public VotingService(DesliderClaudeDbContext db) => _db = db;

    public async Task<Voter> JoinAsync(
        Guid gameNightId,
        string displayName,
        string voterToken,
        Guid? userId = null,
        Guid? visitorId = null,
        CancellationToken ct = default)
    {
        var existing = await _db.Voters.FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct);
        if (existing is not null)
        {
            if (existing.GameNightId != gameNightId)
                throw new InvalidOperationException("Voter token already registered to a different Game Night.");
            existing.DisplayName = displayName;
            if (userId is not null) existing.UserId = userId;
            if (visitorId is not null) existing.VisitorId = visitorId;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var voter = new Voter
        {
            GameNightId = gameNightId,
            DisplayName = displayName,
            VoterToken = voterToken,
            UserId = userId,
            VisitorId = visitorId,
        };
        _db.Voters.Add(voter);
        await _db.SaveChangesAsync(ct);
        return voter;
    }

    public async Task RecordSwipeAsync(string voterToken, Guid gameId, bool yes, CancellationToken ct = default)
    {
        var voter = await _db.Voters.FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct)
            ?? throw new InvalidOperationException("Voter not found.");

        var swipe = await _db.Swipes.FirstOrDefaultAsync(s => s.VoterId == voter.Id && s.GameId == gameId, ct);
        if (swipe is null)
        {
            _db.Swipes.Add(new Swipe { VoterId = voter.Id, GameId = gameId, Yes = yes });
        }
        else
        {
            swipe.Yes = yes;
            swipe.SwipedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GameRanking>> GetRankingAsync(Guid gameNightId, CancellationToken ct = default)
    {
        // Project to an anonymous shape so EF can ORDER BY in SQL; record ctor
        // projections can't be ordered server-side (positional params aren't
        // visible to the translator).
        var rows = await _db.Games
            .Where(g => g.GameNightId == gameNightId)
            .Select(g => new
            {
                g.Id,
                g.Name,
                YesCount = g.Swipes.Count(s => s.Yes),
                NoCount = g.Swipes.Count(s => !s.Yes)
            })
            .OrderByDescending(r => r.YesCount)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        return rows.Select(r => new GameRanking(r.Id, r.Name, r.YesCount, r.NoCount)).ToList();
    }

    public Task<int> GetVoterCountAsync(Guid gameNightId, CancellationToken ct = default)
        => _db.Voters.CountAsync(v => v.GameNightId == gameNightId, ct);

    public async Task<VoterProgress?> GetProgressAsync(string voterToken, CancellationToken ct = default)
    {
        var voter = await _db.Voters
            .Include(v => v.Swipes)
            .FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct);
        if (voter is null) return null;

        var swipes = voter.Swipes.ToDictionary(s => s.GameId, s => s.Yes);
        return new VoterProgress(voter.Id, voter.GameNightId, voter.DisplayName, swipes);
    }

    public async Task<Game?> PickNextGameAsync(string voterToken, Guid? excludeGameId = null, CancellationToken ct = default)
    {
        var voter = await _db.Voters
            .Include(v => v.Swipes)
            .FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct);
        if (voter is null) return null;

        var games = await _db.Games
            .Where(g => g.GameNightId == voter.GameNightId)
            .ToListAsync(ct);
        if (games.Count == 0) return null;

        // Avoid immediately repeating the last-shown game when there's an alternative.
        var pool = games.Count > 1 && excludeGameId is Guid excl
            ? games.Where(g => g.Id != excl).ToList()
            : games;

        var now = DateTimeOffset.UtcNow;
        var swipesByGame = voter.Swipes.ToDictionary(s => s.GameId);

        // Weight function:
        //   unvoted            → 1000 (strongly preferred)
        //   voted t seconds ago → 1 + t / 60 (never zero, grows with age)
        var weights = pool.Select(g =>
        {
            if (!swipesByGame.TryGetValue(g.Id, out var s)) return 1000.0;
            var secondsSince = Math.Max(0, (now - s.SwipedAt).TotalSeconds);
            return 1.0 + secondsSince / 60.0;
        }).ToArray();

        var total = weights.Sum();
        if (total <= 0) return pool[Random.Shared.Next(pool.Count)];

        var roll = Random.Shared.NextDouble() * total;
        var acc = 0.0;
        for (var i = 0; i < pool.Count; i++)
        {
            acc += weights[i];
            if (roll < acc) return pool[i];
        }
        return pool[^1];
    }
}
