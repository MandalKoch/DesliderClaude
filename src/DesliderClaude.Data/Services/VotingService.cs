using DesliderClaude.Core.Entities;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data.Services;

internal sealed class VotingService : IVotingService
{
    private readonly DesliderClaudeDbContext _db;

    public VotingService(DesliderClaudeDbContext db) => _db = db;

    public async Task<Voter> JoinAsync(Guid gameNightId, string displayName, string voterToken, CancellationToken ct = default)
    {
        var existing = await _db.Voters.FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct);
        if (existing is not null)
        {
            if (existing.GameNightId != gameNightId)
                throw new InvalidOperationException("Voter token already registered to a different Game Night.");
            existing.DisplayName = displayName;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var voter = new Voter
        {
            GameNightId = gameNightId,
            DisplayName = displayName,
            VoterToken = voterToken
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
        return await _db.Games
            .Where(g => g.GameNightId == gameNightId)
            .Select(g => new GameRanking(
                g.Id,
                g.Name,
                g.Swipes.Count(s => s.Yes),
                g.Swipes.Count(s => !s.Yes)))
            .OrderByDescending(r => r.YesCount)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);
    }
}
