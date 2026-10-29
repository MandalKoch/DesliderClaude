using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data.Services.Imps;

internal sealed class GatheringService(DesliderClaudeDbContext db, IShareCodeGenerator shareCodes) : IGatheringService
{
    public async Task<GatheringView> CreateAsync(string name, Guid hostUserId, CancellationToken ct = default)
    {
        var gathering = new Gathering
        {
            Name = RequiredText(name, 200, "Give the gathering a name."),
            ShareCode = await NewShareCodeAsync(true, ct),
            CreatedByUserId = hostUserId
        };
        db.Gatherings.Add(gathering);
        await db.SaveChangesAsync(ct);
        return ToView(gathering);
    }

    public async Task<GatheringView?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default)
    {
        var gathering = await db.Gatherings.AsNoTracking()
            .Include(x => x.Members)
            .Include(x => x.Games).ThenInclude(x => x.Interests)
            .Include(x => x.Rounds).ThenInclude(x => x.Games)
            .Include(x => x.PlayedGames).ThenInclude(x => x.GatheringGame)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.ShareCode == shareCode, ct);
        return gathering is null ? null : ToView(gathering);
    }

    public async Task<IReadOnlyList<GatheringSummary>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => await db.Gatherings.AsNoTracking()
            .Where(x => x.CreatedByUserId == userId || x.Members.Any(m => m.UserId == userId))
            .OrderByDescending(x => x.Id)
            .Select(x => new GatheringSummary(x.Id, x.Name, x.ShareCode, x.Games.Count, x.Members.Count, x.CreatedByUserId == userId))
            .ToListAsync(ct);

    public Task<GatheringMemberView?> GetMemberAsync(Guid gatheringId, string memberToken, CancellationToken ct = default)
        => db.GatheringMembers.AsNoTracking()
            .Where(x => x.GatheringId == gatheringId && x.Token == memberToken)
            .Select(x => new GatheringMemberView(x.Id, x.DisplayName, x.UserId))
            .FirstOrDefaultAsync(ct);

    public async Task<GatheringMemberView> JoinAsync(Guid gatheringId, string displayName, string memberToken, Guid? userId = null, CancellationToken ct = default)
    {
        var name = RequiredText(displayName, 40, "Enter your name (up to 40 characters).");
        var token = RequiredText(memberToken, 128, "Your session is missing. Please rejoin the gathering.");
        if (!await db.Gatherings.AnyAsync(x => x.Id == gatheringId, ct))
            throw new InvalidOperationException("Gathering not found.");

        var member = await db.GatheringMembers.FirstOrDefaultAsync(x => x.GatheringId == gatheringId && x.Token == token, ct);
        if (member is null && userId is not null)
            member = await db.GatheringMembers.FirstOrDefaultAsync(x => x.GatheringId == gatheringId && x.UserId == userId, ct);
        if (member is null)
        {
            member = new GatheringMember { GatheringId = gatheringId, DisplayName = name, Token = token, UserId = userId };
            db.GatheringMembers.Add(member);
        }
        else
        {
            member.Token = token;
        }
        await db.SaveChangesAsync(ct);
        return new GatheringMemberView(member.Id, member.DisplayName, member.UserId);
    }

    public async Task AddGameAsync(Guid gatheringId, string memberToken, GameCandidate candidate, CancellationToken ct = default)
    {
        var member = await RequireMemberAsync(gatheringId, memberToken, ct);
        await RequireVotingOpenAsync(gatheringId, ct);
        if (candidate.BggGameId is int bggId)
        {
            var cached = await db.BggGames.AsNoTracking().FirstOrDefaultAsync(g => g.BggGameId == bggId && g.Type == "boardgame", ct)
                ?? throw new InvalidOperationException("Invalid BoardGameGeek base game.");
            candidate = new GameCandidate(cached.Name, bggId, cached.ImageUrl, cached.ThumbnailUrl);
        }
        var name = RequiredText(candidate.Name, 200, "Enter a game name (up to 200 characters).");
        var existing = await db.GatheringGames.Where(x => x.GatheringId == gatheringId)
            .Select(x => new { x.Name, x.BggGameId }).ToListAsync(ct);
        if (existing.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
            || candidate.BggGameId is not null && x.BggGameId == candidate.BggGameId))
            throw new InvalidOperationException("That game is already on the list.");
        db.GatheringGames.Add(new GatheringGame
        {
            GatheringId = gatheringId,
            SuggestedByMemberId = member.Id,
            IsConfirmed = true,
            BringerMemberId = member.Id,
            Name = name,
            BggGameId = candidate.BggGameId,
            ImageUrl = ImageUrl(candidate.ImageUrl),
            ThumbnailUrl = ImageUrl(candidate.ThumbnailUrl)
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveGameAsync(Guid gatheringId, string memberToken, Guid gameId, Guid? requestingUserId = null, CancellationToken ct = default)
    {
        var member = await RequireMemberAsync(gatheringId, memberToken, ct);
        await RequireVotingOpenAsync(gatheringId, ct);
        var game = await RequireGameAsync(gatheringId, gameId, ct);
        var isHost = requestingUserId is not null && await db.Gatherings.AnyAsync(g => g.Id == gatheringId && g.CreatedByUserId == requestingUserId, ct);
        if (game.SuggestedByMemberId != member.Id && !isHost)
            throw new UnauthorizedAccessException("Only the person who suggested this game or the host can remove it.");
        if (await db.Games.AnyAsync(g => g.GatheringGameId == gameId, ct)
            || await db.GatheringPlayedGames.AnyAsync(g => g.GatheringGameId == gameId, ct))
            throw new InvalidOperationException("Games used in a vote or played history must stay in the gathering.");
        db.GatheringGames.Remove(game);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetInterestAsync(Guid gatheringId, string memberToken, Guid gameId, bool interested, CancellationToken ct = default)
    {
        var member = await RequireMemberAsync(gatheringId, memberToken, ct);
        await RequireVotingOpenAsync(gatheringId, ct);
        await RequireGameAsync(gatheringId, gameId, ct);
        var interest = await db.GatheringInterests.FindAsync([gameId, member.Id], ct);
        if (interest is null)
            db.GatheringInterests.Add(new GatheringInterest { GatheringGameId = gameId, MemberId = member.Id, Interested = interested });
        else
            interest.Interested = interested;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetVotingClosedAsync(Guid gatheringId, Guid hostUserId, bool isClosed, CancellationToken ct = default)
    {
        await RequireHostAsync(gatheringId, hostUserId, ct);
        var gathering = await db.Gatherings.SingleAsync(x => x.Id == gatheringId, ct);
        gathering.IsVotingClosed = isClosed;
        await db.SaveChangesAsync(ct);
    }

    public async Task ConfirmGameAsync(Guid gatheringId, Guid hostUserId, Guid gameId, bool isConfirmed, Guid? bringerMemberId, CancellationToken ct = default)
    {
        await RequireHostAsync(gatheringId, hostUserId, ct);
        var game = await RequireGameAsync(gatheringId, gameId, ct);
        if (bringerMemberId is not null && !await db.GatheringMembers.AnyAsync(x => x.Id == bringerMemberId && x.GatheringId == gatheringId, ct))
            throw new InvalidOperationException("Choose someone in this gathering to bring the game.");
        game.IsConfirmed = isConfirmed;
        game.BringerMemberId = isConfirmed ? bringerMemberId : null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<GameNight> StartVoteAsync(Guid gatheringId, Guid hostUserId, string name, IReadOnlyList<Guid> gameIds, CancellationToken ct = default)
    {
        await RequireHostAsync(gatheringId, hostUserId, ct);
        var title = RequiredText(name, 200, "Give this vote a name (up to 200 characters).");
        var selectedIds = gameIds.Distinct().ToArray();
        if (selectedIds.Length < 2)
            throw new InvalidOperationException("Choose at least two games for a vote.");
        var games = await db.GatheringGames.Where(x => x.GatheringId == gatheringId && x.IsConfirmed && selectedIds.Contains(x.Id)).ToListAsync(ct);
        if (games.Count != selectedIds.Length)
            throw new InvalidOperationException("Choose games from this gathering's confirmed bring list.");
        var night = new GameNight
        {
            Name = title,
            ShareCode = await NewShareCodeAsync(false, ct),
            CreatedByUserId = hostUserId,
            GatheringId = gatheringId,
            Games = games.Select(x => new Game
            {
                Name = x.Name,
                BggGameId = x.BggGameId,
                ImageUrl = x.ImageUrl,
                ThumbnailUrl = x.ThumbnailUrl,
                GatheringGameId = x.Id
            }).ToList()
        };
        db.GameNights.Add(night);
        await db.SaveChangesAsync(ct);
        return night;
    }

    public async Task MarkPlayedAsync(Guid gatheringId, Guid hostUserId, Guid gameId, CancellationToken ct = default)
    {
        await RequireHostAsync(gatheringId, hostUserId, ct);
        var game = await RequireGameAsync(gatheringId, gameId, ct);
        if (!game.IsConfirmed)
            throw new InvalidOperationException("Only games on the bring list can be marked played.");
        db.GatheringPlayedGames.Add(new GatheringPlayedGame { GatheringId = gatheringId, GatheringGameId = gameId });
        await db.SaveChangesAsync(ct);
    }

    private async Task RequireVotingOpenAsync(Guid gatheringId, CancellationToken ct)
    {
        if (await db.Gatherings.AnyAsync(x => x.Id == gatheringId && x.IsVotingClosed, ct))
            throw new InvalidOperationException("Voting is closed. The host can reopen it to add games or change votes.");
    }

    private async Task<GatheringMember> RequireMemberAsync(Guid gatheringId, string token, CancellationToken ct)
        => await db.GatheringMembers.FirstOrDefaultAsync(x => x.GatheringId == gatheringId && x.Token == token, ct)
            ?? throw new UnauthorizedAccessException("Join this gathering first.");

    private async Task RequireHostAsync(Guid gatheringId, Guid userId, CancellationToken ct)
    {
        var gathering = await db.Gatherings.FirstOrDefaultAsync(x => x.Id == gatheringId, ct)
            ?? throw new InvalidOperationException("Gathering not found.");
        if (gathering.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("Only the host can do that.");
    }

    private async Task<GatheringGame> RequireGameAsync(Guid gatheringId, Guid gameId, CancellationToken ct)
        => await db.GatheringGames.FirstOrDefaultAsync(x => x.Id == gameId && x.GatheringId == gatheringId, ct)
            ?? throw new InvalidOperationException("That game is not in this gathering.");

    private async Task<string> NewShareCodeAsync(bool gathering, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = shareCodes.Generate();
            if (gathering ? !await db.Gatherings.AnyAsync(x => x.ShareCode == code, ct) : !await db.GameNights.AnyAsync(x => x.ShareCode == code, ct))
                return code;
        }
        throw new InvalidOperationException("Couldn't create a share link. Please try again.");
    }

    private static string RequiredText(string? value, int maxLength, string error)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > maxLength)
            throw new InvalidOperationException(error);
        return text;
    }

    private static string? ImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length > 512 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Invalid game image URL.");
        return value;
    }

    private static GatheringView ToView(Gathering x)
        => new(x.Id, x.Name, x.ShareCode, x.CreatedByUserId,
            x.Members.OrderBy(m => m.DisplayName).Select(m => new GatheringMemberView(m.Id, m.DisplayName, m.UserId)).ToList(),
            x.Games.OrderBy(g => g.Name).Select(g => new GatheringGameView(g.Id, g.Name, g.BggGameId, g.ImageUrl, g.ThumbnailUrl,
                g.SuggestedByMemberId, g.IsConfirmed, g.BringerMemberId,
                g.Interests.Select(i => new GatheringInterestView(i.MemberId, i.Interested)).ToList(),
                x.Rounds.Any(n => n.Games.Any(r => r.GatheringGameId == g.Id)))).ToList(),
            x.Rounds.OrderByDescending(n => n.CreatedAt).Select(n => new GatheringRoundView(n.Id, n.Name, n.ShareCode, n.IsClosed, n.CreatedAt)).ToList(),
            x.PlayedGames.OrderByDescending(p => p.PlayedAt).Select(p => new GatheringPlayedGameView(p.Id, p.GatheringGameId, p.GatheringGame.Name, p.PlayedAt)).ToList(),
            x.IsVotingClosed);
}
