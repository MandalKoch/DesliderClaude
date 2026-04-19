using System.Security.Cryptography;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data.Services.Imps;

internal sealed class GameNightService : IGameNightService
{
    private readonly DesliderClaudeDbContext _db;
    private readonly IShareCodeGenerator _shareCodes;

    public GameNightService(DesliderClaudeDbContext db, IShareCodeGenerator shareCodes)
    {
        _db = db;
        _shareCodes = shareCodes;
    }

    public async Task<GameNight> CreateAsync(
        string name,
        DateOnly? targetDate,
        IEnumerable<string> gameNames,
        Guid? createdByUserId = null,
        CancellationToken ct = default)
    {
        var shareCode = await GenerateUniqueShareCodeAsync(ct);
        var hostToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var night = new GameNight
        {
            Name = name,
            TargetDate = targetDate,
            ShareCode = shareCode,
            HostToken = hostToken,
            CreatedByUserId = createdByUserId,
            Games = gameNames.Select(n => new Game { Name = n }).ToList()
        };

        _db.GameNights.Add(night);
        await _db.SaveChangesAsync(ct);
        return night;
    }

    public async Task<IReadOnlyList<UserNightSummary>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // One SQL query: every night where the user is host OR voter, plus their swipe count.
        var rows = await _db.GameNights
            .Where(n => n.CreatedByUserId == userId || n.Voters.Any(v => v.UserId == userId))
            .Select(n => new
            {
                n.ShareCode,
                n.Name,
                n.TargetDate,
                n.IsClosed,
                n.ClosedAt,
                n.CreatedAt,
                IsHost = n.CreatedByUserId == userId,
                IsVoter = n.Voters.Any(v => v.UserId == userId),
                GameCount = n.Games.Count,
                SwipesByViewer = n.Voters
                    .Where(v => v.UserId == userId)
                    .SelectMany(v => v.Swipes)
                    .Select(s => s.GameId)
                    .Distinct()
                    .Count(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new UserNightSummary(
                r.ShareCode, r.Name, r.TargetDate, r.IsClosed, r.ClosedAt, r.CreatedAt,
                r.IsHost, r.IsVoter, r.GameCount, r.SwipesByViewer))
            .ToList();
    }

    public Task<GameNight?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default)
        => _db.GameNights
            .Include(n => n.Games)
            .FirstOrDefaultAsync(n => n.ShareCode == shareCode, ct);

    public async Task CloseAsync(Guid gameNightId, string hostToken, CancellationToken ct = default)
    {
        var night = await _db.GameNights.FirstOrDefaultAsync(n => n.Id == gameNightId, ct)
            ?? throw new InvalidOperationException("Game Night not found.");
        if (night.HostToken != hostToken)
            throw new UnauthorizedAccessException("Host token does not match.");
        if (night.IsClosed) return;

        night.IsClosed = true;
        night.ClosedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> GenerateUniqueShareCodeAsync(CancellationToken ct)
    {
        for (int i = 0; i < 10; i++)
        {
            var code = _shareCodes.Generate();
            if (!await _db.GameNights.AnyAsync(n => n.ShareCode == code, ct)) return code;
        }
        throw new InvalidOperationException("Could not generate a unique share code after 10 attempts.");
    }
}
