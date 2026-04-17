using System.Security.Cryptography;
using DesliderClaude.Core.Entities;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data.Services;

internal sealed class GameNightService : IGameNightService
{
    private readonly DesliderClaudeDbContext _db;
    private readonly IShareCodeGenerator _shareCodes;

    public GameNightService(DesliderClaudeDbContext db, IShareCodeGenerator shareCodes)
    {
        _db = db;
        _shareCodes = shareCodes;
    }

    public async Task<GameNight> CreateAsync(string name, DateOnly? targetDate, IEnumerable<string> gameNames, CancellationToken ct = default)
    {
        var shareCode = await GenerateUniqueShareCodeAsync(ct);
        var hostToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var night = new GameNight
        {
            Name = name,
            TargetDate = targetDate,
            ShareCode = shareCode,
            HostToken = hostToken,
            Games = gameNames.Select(n => new Game { Name = n }).ToList()
        };

        _db.GameNights.Add(night);
        await _db.SaveChangesAsync(ct);
        return night;
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
