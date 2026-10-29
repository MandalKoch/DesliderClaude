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
            NightCount: await _db.GameNights.CountAsync(ct),
            OpenNightCount: await _db.GameNights.CountAsync(n => !n.IsClosed, ct),
            ClosedNightCount: await _db.GameNights.CountAsync(n => n.IsClosed, ct),
            VoterCount: await _db.Voters.CountAsync(ct),
            SwipeCount: swipeTimes.Count,
            SwipesLast24h: swipeTimes.Count(t => t >= since),
            GatheringCount: await _db.Gatherings.CountAsync(ct),
            OpenGatheringCount: await _db.Gatherings.CountAsync(g => !g.IsVotingClosed, ct),
            ClosedGatheringCount: await _db.Gatherings.CountAsync(g => g.IsVotingClosed, ct),
            GatheringMemberCount: await _db.GatheringMembers.CountAsync(ct),
            GatheringGameCount: await _db.GatheringGames.CountAsync(ct),
            GatheringVoteCount: await _db.GatheringInterests.CountAsync(ct));
    }

    public async Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(CancellationToken ct = default)
    {
        // External providers in a second query, keyed by UserId — avoids a
        // navigation-inside-Select that SQLite won't translate.
        var providers = (await _db.ExternalLogins
                .Select(l => new { l.UserId, l.Provider })
                .ToListAsync(ct))
            .GroupBy(l => l.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(l => l.Provider).Distinct().ToList());

        var rows = await _db.Users
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.CreatedAt,
                HasPassword = u.PasswordHash != null,
                NightsHosted = _db.GameNights.Count(n => n.CreatedByUserId == u.Id),
                NightsVotedIn = _db.Voters.Where(v => v.UserId == u.Id).Select(v => v.GameNightId).Distinct().Count(),
                SwipeCount = _db.Voters.Where(v => v.UserId == u.Id).SelectMany(v => v.Swipes).Count(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new AdminUserRow(
                r.Id, r.Username, r.CreatedAt, r.HasPassword,
                providers.TryGetValue(r.Id, out var p) ? p : Array.Empty<string>(),
                r.NightsHosted, r.NightsVotedIn, r.SwipeCount))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
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

    private IQueryable<AdminGatheringRow> GatheringRows(string? shareCode = null) => _db.Gatherings.AsNoTracking()
        .Where(g => shareCode == null || g.ShareCode == shareCode)
        .Select(g => new AdminGatheringRow(
            g.Id, g.ShareCode, g.Name,
            _db.Users.Where(u => u.Id == g.CreatedByUserId).Select(u => u.Username).FirstOrDefault(),
            g.IsVotingClosed, g.CreatedAt, g.Members.Count, g.Games.Count,
            g.Games.SelectMany(game => game.Interests).Count()));

    public async Task<IReadOnlyList<AdminGatheringRow>> ListGatheringsAsync(CancellationToken ct = default)
        => (await GatheringRows().ToListAsync(ct)).OrderByDescending(g => g.CreatedAt).ToList();

    public async Task<AdminGatheringDetail?> GetGatheringDetailAsync(string shareCode, CancellationToken ct = default)
    {
        var gathering = await GatheringRows(shareCode).SingleOrDefaultAsync(ct);
        if (gathering is null) return null;
        var games = await _db.GatheringGames.AsNoTracking().Where(g => g.GatheringId == gathering.Id)
            .OrderBy(g => g.Name)
            .Select(g => new AdminGatheringGame(g.Id, g.Name, g.BringerMember == null ? null : g.BringerMember.DisplayName))
            .ToListAsync(ct);
        var members = await _db.GatheringMembers.AsNoTracking().Where(m => m.GatheringId == gathering.Id)
            .OrderBy(m => m.DisplayName)
            .Select(m => new
            {
                m.Id,
                m.DisplayName,
                Username = _db.Users.Where(u => u.Id == m.UserId).Select(u => u.Username).FirstOrDefault()
            }).ToListAsync(ct);
        var interests = await _db.GatheringInterests.AsNoTracking()
            .Where(i => i.GatheringGame.GatheringId == gathering.Id)
            .Select(i => new { i.GatheringGameId, i.MemberId, i.Interested }).ToListAsync(ct);
        var choices = interests.ToDictionary(i => (i.GatheringGameId, i.MemberId), i => i.Interested);
        var participants = members.Select(m => new AdminGatheringMember(m.Id, m.DisplayName, m.Username,
            games.Select(g => new AdminGameChoice(g.Id, g.Name,
                choices.TryGetValue((g.Id, m.Id), out var yes) ? yes : (bool?)null)).ToList())).ToList();
        var rounds = await _db.GameNights.AsNoTracking().Where(n => n.GatheringId == gathering.Id)
            .Select(n => new GatheringRoundView(n.Id, n.Name, n.ShareCode, n.IsClosed, n.CreatedAt)).ToListAsync(ct);
        var played = await _db.GatheringPlayedGames.AsNoTracking().Where(p => p.GatheringId == gathering.Id)
            .Select(p => new GatheringPlayedGameView(p.Id, p.GatheringGameId, p.GatheringGame.Name, p.PlayedAt)).ToListAsync(ct);
        return new AdminGatheringDetail(gathering, games, participants,
            rounds.OrderByDescending(r => r.CreatedAt).ToList(), played.OrderByDescending(p => p.PlayedAt).ToList());
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

    public async Task SetUserPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("User not found.");
        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await _db.SaveChangesAsync(ct);
        LogUserPasswordSet(userId, user.Username);
    }

    public async Task DeleteNightAsync(Guid nightId, CancellationToken ct = default)
    {
        var night = await _db.GameNights.FirstOrDefaultAsync(n => n.Id == nightId, ct)
            ?? throw new InvalidOperationException("Night not found.");
        _db.GameNights.Remove(night);
        await _db.SaveChangesAsync(ct);
        LogNightDeleted(nightId, night.ShareCode);
    }

    public async Task<IReadOnlyList<AdminVoterRow>> ListVotersForNightAsync(Guid nightId, CancellationToken ct = default)
    {
        var games = await _db.Games.AsNoTracking().Where(g => g.GameNightId == nightId)
            .OrderBy(g => g.Name).Select(g => new { g.Id, g.Name }).ToListAsync(ct);
        var voters = await _db.Voters.AsNoTracking().Where(v => v.GameNightId == nightId)
            .Select(v => new
            {
                v.Id,
                v.DisplayName,
                Username = v.User == null ? null : v.User.Username,
                Swipes = v.Swipes.Select(s => new { s.GameId, s.Yes }).ToList()
            }).ToListAsync(ct);
        return voters.OrderBy(v => v.DisplayName).Select(v =>
        {
            var choices = v.Swipes.ToDictionary(s => s.GameId, s => s.Yes);
            return new AdminVoterRow(v.Id, v.DisplayName, v.Username, v.Swipes.Count,
                v.Swipes.Count(s => s.Yes), v.Swipes.Count(s => !s.Yes),
                games.Select(g => new AdminGameChoice(g.Id, g.Name,
                    choices.TryGetValue(g.Id, out var yes) ? yes : (bool?)null)).ToList());
        }).ToList();
    }

    public async Task DeleteVoterAsync(Guid voterId, CancellationToken ct = default)
    {
        var voter = await _db.Voters.FirstOrDefaultAsync(v => v.Id == voterId, ct)
            ?? throw new InvalidOperationException("Voter not found.");
        _db.Voters.Remove(voter);
        await _db.SaveChangesAsync(ct);
        LogVoterDeleted(voterId, voter.DisplayName, voter.GameNightId);
    }

    public async Task CloseNightAsync(Guid nightId, CancellationToken ct = default)
    {
        var night = await _db.GameNights.FirstOrDefaultAsync(n => n.Id == nightId, ct)
            ?? throw new InvalidOperationException("Night not found.");
        if (night.IsClosed) return;
        night.IsClosed = true;
        night.ClosedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        LogNightClosed(nightId, night.ShareCode);
    }

    public async Task ReopenNightAsync(Guid nightId, CancellationToken ct = default)
    {
        var night = await _db.GameNights.FirstOrDefaultAsync(n => n.Id == nightId, ct)
            ?? throw new InvalidOperationException("Night not found.");
        if (!night.IsClosed) return;
        night.IsClosed = false;
        night.ClosedAt = null;
        await _db.SaveChangesAsync(ct);
        LogNightReopened(nightId, night.ShareCode);
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
        Message = "Admin deleted user {UserId} ({Username})")]
    private partial void LogUserDeleted(Guid userId, string username);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning,
        Message = "Admin deleted game night {NightId} ({ShareCode})")]
    private partial void LogNightDeleted(Guid nightId, string shareCode);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning,
        Message = "Admin closed game night {NightId} ({ShareCode})")]
    private partial void LogNightClosed(Guid nightId, string shareCode);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning,
        Message = "Admin deleted voter {VoterId} ({DisplayName}) from night {NightId}")]
    private partial void LogVoterDeleted(Guid voterId, string displayName, Guid nightId);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Warning,
        Message = "Admin re-opened game night {NightId} ({ShareCode})")]
    private partial void LogNightReopened(Guid nightId, string shareCode);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Warning,
        Message = "Admin set password for user {UserId} ({Username})")]
    private partial void LogUserPasswordSet(Guid userId, string username);
}
