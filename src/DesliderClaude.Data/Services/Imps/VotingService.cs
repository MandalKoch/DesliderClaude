using System.Security.Cryptography;
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
        CancellationToken ct = default)
    {
        var name = RequiredName(displayName);
        ValidateToken(voterToken);
        if (!await _db.GameNights.AnyAsync(n => n.Id == gameNightId, ct))
            throw new InvalidOperationException("Game Night not found.");
        var tokenOwner = await _db.Voters.FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct);
        if (tokenOwner is not null && (tokenOwner.GameNightId != gameNightId || tokenOwner.UserId != userId))
            throw new UnauthorizedAccessException("Choose the guest explicitly or sign in to the account that owns this session.");
        var existing = userId is not null
            ? await _db.Voters.FirstOrDefaultAsync(v => v.GameNightId == gameNightId && v.UserId == userId, ct)
            : tokenOwner;
        if (existing is null && userId is null)
        {
            var matches = await _db.Voters.Where(v => v.GameNightId == gameNightId).ToListAsync(ct);
            var guests = matches.Where(v => v.UserId is null && string.Equals(v.DisplayName.Trim(), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (guests.Count > 1) throw new InvalidOperationException("Several guests use that name. Choose your existing guest from the list.");
            existing = guests.SingleOrDefault();
            if (existing is null && matches.Any(v => v.UserId is not null && string.Equals(v.DisplayName.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("That name belongs to an account. Sign in to use it.");
        }
        if (existing is not null)
        {
            existing.VoterToken = voterToken;
            await _db.SaveChangesAsync(ct);
            return existing;
        }
        var voter = new Voter { GameNightId = gameNightId, DisplayName = name, VoterToken = voterToken, UserId = userId };
        _db.Voters.Add(voter);
        await _db.SaveChangesAsync(ct);
        return voter;
    }

    public async Task<IReadOnlyList<ParticipantIdentity>> ListParticipantsAsync(Guid gameNightId, CancellationToken ct = default)
        => await _db.Voters.AsNoTracking().Where(v => v.GameNightId == gameNightId)
            .OrderBy(v => v.DisplayName)
            .Select(v => new ParticipantIdentity(v.Id, v.DisplayName, v.UserId != null)).ToListAsync(ct);

    public async Task<Voter> RejoinGuestAsync(Guid gameNightId, Guid voterId, string voterToken, Guid? userId = null, CancellationToken ct = default)
    {
        ValidateToken(voterToken);
        var voter = await _db.Voters.SingleOrDefaultAsync(v => v.Id == voterId && v.GameNightId == gameNightId, ct)
            ?? throw new InvalidOperationException("Guest not found in this Game Night.");
        if (voter.UserId is not null) throw new UnauthorizedAccessException("Sign in to the account that owns this participant.");
        if (userId is not null && await _db.Voters.AnyAsync(v => v.GameNightId == gameNightId && v.UserId == userId, ct))
            throw new InvalidOperationException("Your account already has a participant in this Game Night.");
        if (await _db.Voters.AnyAsync(v => v.VoterToken == voterToken && v.Id != voterId, ct))
            throw new InvalidOperationException("Start a new session to rejoin this guest.");
        voter.VoterToken = voterToken;
        voter.UserId = userId;
        await _db.SaveChangesAsync(ct);
        return voter;
    }

    public async Task RecordSwipeAsync(string voterToken, Guid gameId, bool yes, Guid? userId = null, CancellationToken ct = default)
    {
        var voter = await _db.Voters.FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct)
            ?? throw new InvalidOperationException("Voter not found.");

        RequireOwner(voter, userId);
        await RequireWritableGameAsync(voter.GameNightId, gameId, ct);
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

    public async Task RemoveSwipeAsync(string voterToken, Guid gameId, Guid? userId = null, CancellationToken ct = default)
    {
        var voter = await _db.Voters.FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct)
            ?? throw new InvalidOperationException("Voter not found.");
        RequireOwner(voter, userId);
        await RequireWritableGameAsync(voter.GameNightId, gameId, ct);
        var swipe = await _db.Swipes.FirstOrDefaultAsync(s => s.VoterId == voter.Id && s.GameId == gameId, ct);
        if (swipe is null) return;
        _db.Swipes.Remove(swipe);
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
                g.ImageUrl,
                g.ThumbnailUrl,
                YesCount = g.Swipes.Count(s => s.Yes),
                NoCount = g.Swipes.Count(s => !s.Yes)
            })
            .OrderByDescending(r => r.YesCount)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        return rows.Select(r => new GameRanking(r.Id, r.Name, r.YesCount, r.NoCount, r.ImageUrl, r.ThumbnailUrl)).ToList();
    }

    public Task<int> GetVoterCountAsync(Guid gameNightId, CancellationToken ct = default)
        => _db.Voters.CountAsync(v => v.GameNightId == gameNightId, ct);

    public async Task<IReadOnlyList<VoterStatus>> ListVoterStatusesAsync(Guid gameNightId, CancellationToken ct = default)
    {
        // SQLite can't translate Max over DateTimeOffset, so we materialise the
        // raw swipe times and reduce in memory. The voter set per night is small.
        var rows = await _db.Voters
            .Where(v => v.GameNightId == gameNightId)
            .Select(v => new
            {
                v.Id,
                v.DisplayName,
                v.VoterToken,
                SwipeTimes = v.Swipes.Select(s => s.SwipedAt).ToList(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new VoterStatus(
                r.Id,
                r.DisplayName,
                r.VoterToken,
                r.SwipeTimes.Count,
                r.SwipeTimes.Count == 0 ? null : r.SwipeTimes.Max()))
            .OrderByDescending(s => s.SwipesCast)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<VoterPublicStatus>> ListPublicVoterStatusesAsync(Guid gameNightId, CancellationToken ct = default)
    {
        var rows = await _db.Voters
            .Where(v => v.GameNightId == gameNightId)
            .Select(v => new
            {
                v.DisplayName,
                SwipeTimes = v.Swipes.Select(s => s.SwipedAt).ToList(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new VoterPublicStatus(
                r.DisplayName,
                r.SwipeTimes.Count,
                r.SwipeTimes.Count == 0 ? null : r.SwipeTimes.Max()))
            .OrderByDescending(s => s.SwipesCast)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task RemoveVoterAsync(Guid voterId, Guid gameNightId, CancellationToken ct = default)
    {
        var voter = await _db.Voters.FirstOrDefaultAsync(v => v.Id == voterId && v.GameNightId == gameNightId, ct);
        if (voter is null) return;
        // Swipes cascade-delete via the Voter→Swipes FK.
        _db.Voters.Remove(voter);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Voter> CreateInviteAsync(Guid gameNightId, string displayName, CancellationToken ct = default)
    {
        return await JoinAsync(gameNightId, displayName, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), ct: ct);
    }

    public async Task<VoterProgress?> GetProgressAsync(string voterToken, Guid? userId = null, CancellationToken ct = default)
    {
        var voter = await _db.Voters
            .Include(v => v.Swipes)
            .FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct);
        if (voter is null || voter.UserId is not null && voter.UserId != userId) return null;

        var swipes = voter.Swipes.ToDictionary(s => s.GameId, s => s.Yes);
        return new VoterProgress(voter.Id, voter.GameNightId, voter.DisplayName, voter.UserId, swipes);
    }

    public async Task<Game?> PickNextGameAsync(string voterToken, Guid? excludeGameId = null, Guid? userId = null, CancellationToken ct = default)
    {
        var voter = await _db.Voters
            .Include(v => v.Swipes)
            .FirstOrDefaultAsync(v => v.VoterToken == voterToken, ct);
        if (voter is null || voter.UserId is not null && voter.UserId != userId) return null;

        var swipedGameIds = voter.Swipes.Select(s => s.GameId).ToHashSet();

        // One vote per game: only unvoted games are picks. Changing/removing an
        // existing vote happens from the dedicated /votes page.
        var unvoted = await _db.Games
            .Where(g => g.GameNightId == voter.GameNightId && !swipedGameIds.Contains(g.Id))
            .ToListAsync(ct);
        if (unvoted.Count == 0) return null;

        // Avoid immediately re-showing the one we just swiped (shouldn't happen now
        // that swiped games are excluded, but keep the safety for concurrent edits).
        var pool = unvoted.Count > 1 && excludeGameId is Guid excl
            ? unvoted.Where(g => g.Id != excl).ToList()
            : unvoted;

        return pool[Random.Shared.Next(pool.Count)];
    }
    private static string RequiredName(string displayName)
    {
        var name = (displayName ?? string.Empty).Trim();
        if (name.Length is < 1 or > 40) throw new InvalidOperationException("Enter your name (up to 40 characters).");
        return name;
    }

    private static void ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            throw new InvalidOperationException("Your session is missing. Please rejoin the Game Night.");
    }

    private static void RequireOwner(Voter voter, Guid? userId)
    {
        if (voter.UserId is not null && voter.UserId != userId)
            throw new UnauthorizedAccessException("Sign in to the account that owns this participant.");
    }

    private async Task RequireWritableGameAsync(Guid nightId, Guid gameId, CancellationToken ct)
    {
        if (!await _db.Games.AnyAsync(g => g.Id == gameId && g.GameNightId == nightId, ct))
            throw new InvalidOperationException("Choose a game from this Game Night.");
        if (!await _db.GameNights.AnyAsync(n => n.Id == nightId && !n.IsClosed, ct))
            throw new InvalidOperationException("Voting is closed.");
    }

}
