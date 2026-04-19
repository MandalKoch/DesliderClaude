using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DesliderClaude.Data.Services.Imps;

internal sealed partial class AdminService : IAdminService
{
    private readonly DesliderClaudeDbContext _db;
    private readonly ILogger<AdminService> _logger;

    public AdminService(DesliderClaudeDbContext db, ILogger<AdminService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AdminOverview> GetOverviewAsync(CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);

        // SQLite's EF provider can't translate `DateTimeOffset >= ...` directly —
        // pull just the timestamps and filter in memory. At our scale (a few
        // nights × a few voters) this is fine; revisit if swipe volume explodes.
        var swipeTimes = await _db.Swipes.Select(s => s.SwipedAt).ToListAsync(ct);

        return new AdminOverview(
            UserCount: await _db.Users.CountAsync(ct),
            VisitorCount: await _db.Visitors.CountAsync(ct),
            NightCount: await _db.GameNights.CountAsync(ct),
            OpenNightCount: await _db.GameNights.CountAsync(n => !n.IsClosed, ct),
            ClosedNightCount: await _db.GameNights.CountAsync(n => n.IsClosed, ct),
            VoterCount: await _db.Voters.CountAsync(ct),
            SwipeCount: swipeTimes.Count,
            SwipesLast24h: swipeTimes.Count(t => t >= since));
    }

    public async Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(CancellationToken ct = default)
    {
        // SQLite can't ORDER BY DateTimeOffset — materialize, then sort in memory.
        var rows = await _db.Users
            .Select(u => new AdminUserRow(
                u.Id,
                u.Username,
                u.CreatedAt,
                _db.GameNights.Count(n => n.CreatedByUserId == u.Id),
                _db.Voters.Where(v => v.UserId == u.Id).Select(v => v.GameNightId).Distinct().Count(),
                _db.Voters.Where(v => v.UserId == u.Id).SelectMany(v => v.Swipes).Count()))
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<AdminNightRow>> ListNightsAsync(CancellationToken ct = default)
    {
        var rows = await _db.GameNights
            .Select(n => new AdminNightRow(
                n.Id,
                n.ShareCode,
                n.Name,
                n.TargetDate,
                n.IsClosed,
                n.CreatedAt,
                n.ClosedAt,
                n.CreatedByUser == null ? null : n.CreatedByUser.Username,
                n.Games.Count,
                n.Voters.Count,
                n.Voters.SelectMany(v => v.Swipes).Count()))
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<GamePopularityRow>> TopGamesAsync(int take = 10, CancellationToken ct = default)
    {
        // SQL does per-game aggregation; client merges rows by name so "Catan on
        // Friday" and "Catan on Saturday" count together.
        var perGame = await _db.Games
            .Select(g => new
            {
                g.Name,
                YesCount = g.Swipes.Count(s => s.Yes),
                NoCount = g.Swipes.Count(s => !s.Yes),
            })
            .ToListAsync(ct);

        return perGame
            .GroupBy(g => g.Name)
            .Select(grp => new GamePopularityRow(
                grp.Key,
                grp.Sum(g => g.YesCount),
                grp.Sum(g => g.NoCount)))
            .OrderByDescending(r => r.YesCount - r.NoCount)
            .ThenByDescending(r => r.TotalVotes)
            .Take(take)
            .ToList();
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("User not found.");
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        LogUserDeleted(userId, user.Username);
    }

    public async Task DeleteNightAsync(Guid nightId, CancellationToken ct = default)
    {
        var night = await _db.GameNights.FirstOrDefaultAsync(n => n.Id == nightId, ct)
            ?? throw new InvalidOperationException("Night not found.");
        _db.GameNights.Remove(night);
        await _db.SaveChangesAsync(ct);
        LogNightDeleted(nightId, night.ShareCode);
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "Admin deleted user {UserId} ({Username})")]
    private partial void LogUserDeleted(Guid userId, string username);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning,
        Message = "Admin deleted game night {NightId} ({ShareCode})")]
    private partial void LogNightDeleted(Guid nightId, string shareCode);
}
