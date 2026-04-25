using System.Text.Json;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data.Services.Imps;

internal sealed class BggImportService : IBggImportService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DesliderClaudeDbContext _db;
    private readonly IBggClient _bgg;

    public BggImportService(DesliderClaudeDbContext db, IBggClient bgg)
    {
        _db = db;
        _bgg = bgg;
    }

    public async Task<BggImport> ImportGeekListAsync(Guid userId, int geekListId, CancellationToken ct = default)
    {
        var fetched = await _bgg.FetchGeekListAsync(geekListId, ct);
        var refStr = geekListId.ToString();

        var existing = await _db.BggImports.FirstOrDefaultAsync(
            i => i.UserId == userId && i.SourceType == BggImportSource.GeekList && i.SourceRef == refStr, ct);

        if (existing is not null)
        {
            await ReplaceItemsAsync(existing, fetched.GameIds, fetched.Name, ct);
            return existing;
        }

        var import = new BggImport
        {
            UserId = userId,
            SourceType = BggImportSource.GeekList,
            SourceRef = refStr,
            Name = fetched.Name,
        };
        _db.BggImports.Add(import);
        await ReplaceItemsAsync(import, fetched.GameIds, fetched.Name, ct);
        return import;
    }

    public async Task<BggImport> ImportCollectionAsync(Guid userId, string username, CancellationToken ct = default)
    {
        var fetched = await _bgg.FetchCollectionAsync(username, ct);
        var label = $"@{fetched.Username}";

        var existing = await _db.BggImports.FirstOrDefaultAsync(
            i => i.UserId == userId && i.SourceType == BggImportSource.Collection && i.SourceRef == fetched.Username, ct);

        if (existing is not null)
        {
            await ReplaceItemsAsync(existing, fetched.GameIds, label, ct);
            return existing;
        }

        var import = new BggImport
        {
            UserId = userId,
            SourceType = BggImportSource.Collection,
            SourceRef = fetched.Username,
            Name = label,
        };
        _db.BggImports.Add(import);
        await ReplaceItemsAsync(import, fetched.GameIds, label, ct);
        return import;
    }

    public async Task RefreshAsync(Guid importId, Guid userId, CancellationToken ct = default)
    {
        // Don't .Include(Items) here — ReplaceItemsAsync wipes them via
        // ExecuteDeleteAsync (SQL-only) and rebuilds. Loading them into the
        // change tracker first means re-adding rows with the same composite
        // key throws "another instance with the same key is already tracked".
        var import = await _db.BggImports
            .FirstOrDefaultAsync(i => i.Id == importId && i.UserId == userId, ct)
            ?? throw new InvalidOperationException("Import not found.");

        if (import.SourceType == BggImportSource.GeekList)
        {
            var id = int.Parse(import.SourceRef);
            var fetched = await _bgg.FetchGeekListAsync(id, ct);
            await ReplaceItemsAsync(import, fetched.GameIds, fetched.Name, ct);
        }
        else
        {
            var fetched = await _bgg.FetchCollectionAsync(import.SourceRef, ct);
            await ReplaceItemsAsync(import, fetched.GameIds, $"@{fetched.Username}", ct);
        }
    }

    public async Task<IReadOnlyList<BggImportView>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // SQLite can't translate ORDER BY on DateTimeOffset — materialise first,
        // sort in memory. Same pattern as AdminService.
        var rows = await _db.BggImports
            .Where(i => i.UserId == userId)
            .Select(i => new BggImportView(
                i.Id,
                i.SourceType,
                i.SourceRef,
                i.Name,
                i.Items.Count,
                i.LastRefreshedAt))
            .ToListAsync(ct);

        return rows
            .OrderByDescending(v => v.LastRefreshedAt)
            .ToList();
    }

    public async Task DeleteAsync(Guid importId, Guid userId, CancellationToken ct = default)
    {
        var import = await _db.BggImports.FirstOrDefaultAsync(i => i.Id == importId && i.UserId == userId, ct);
        if (import is null) return;
        _db.BggImports.Remove(import);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BggCandidateGame>> ListCandidatesAsync(
        Guid userId,
        IReadOnlyList<Guid> importIds,
        CancellationToken ct = default)
    {
        if (importIds.Count == 0) return Array.Empty<BggCandidateGame>();

        // Only consider imports the caller actually owns.
        var ownedIds = await _db.BggImports
            .Where(i => i.UserId == userId && importIds.Contains(i.Id))
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (ownedIds.Count == 0) return Array.Empty<BggCandidateGame>();

        // Distinct BggGame IDs referenced by those imports → hydrate from cache.
        var gameIds = await _db.BggImportItems
            .Where(x => ownedIds.Contains(x.BggImportId))
            .Select(x => x.BggGameId)
            .Distinct()
            .ToListAsync(ct);

        if (gameIds.Count == 0) return Array.Empty<BggCandidateGame>();

        var rows = await _db.BggGames
            .Where(g => gameIds.Contains(g.BggGameId) && g.Type == "boardgame")
            .Select(g => new
            {
                g.BggGameId,
                g.Name,
                g.ImageUrl,
                g.ThumbnailUrl,
                g.MinPlayers,
                g.MaxPlayers,
                g.MinPlayTimeMinutes,
                g.MaxPlayTimeMinutes,
                g.RecommendedPlayerCountsJson,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new BggCandidateGame(
                r.BggGameId,
                r.Name,
                r.ImageUrl,
                r.ThumbnailUrl,
                r.MinPlayers,
                r.MaxPlayers,
                r.MinPlayTimeMinutes,
                r.MaxPlayTimeMinutes,
                ParseRecommended(r.RecommendedPlayerCountsJson)))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<BggPlayerCountVote> ParseRecommended(string? json)
    {
        if (string.IsNullOrEmpty(json)) return Array.Empty<BggPlayerCountVote>();
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<BggPlayerCountVote>>(json, Json)
                ?? Array.Empty<BggPlayerCountVote>();
        }
        catch (JsonException)
        {
            return Array.Empty<BggPlayerCountVote>();
        }
    }

    /// <summary>Core upsert: fetches/refreshes BggGame rows for <paramref name="gameIds"/>,
    /// replaces the import's items list, and bumps LastRefreshedAt. Expansions
    /// and accessories are fetched (so we know what they are) but skipped from
    /// the import's <see cref="BggImportItem"/> list — only "boardgame" items
    /// surface as candidates.</summary>
    private async Task ReplaceItemsAsync(BggImport import, IReadOnlyList<int> gameIds, string displayName, CancellationToken ct)
    {
        import.Name = displayName;
        import.LastRefreshedAt = DateTimeOffset.UtcNow;

        await _db.BggImportItems.Where(x => x.BggImportId == import.Id).ExecuteDeleteAsync(ct);

        if (gameIds.Count == 0)
        {
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Existing BggGame rows for these IDs. Rows with Type == null pre-date
        // the column, so we re-fetch to backfill — every other field gets
        // refreshed at the same time. The "forever cache" rule still applies:
        // if a row is fully populated, we don't hit BGG for it again.
        var existing = await _db.BggGames
            .Where(g => gameIds.Contains(g.BggGameId))
            .ToListAsync(ct);
        var existingById = existing.ToDictionary(g => g.BggGameId);

        var needsFetch = gameIds
            .Where(id => !existingById.TryGetValue(id, out var g) || g.Type is null)
            .Distinct()
            .ToList();

        if (needsFetch.Count > 0)
        {
            var things = await _bgg.FetchThingsAsync(needsFetch, ct);
            foreach (var t in things)
            {
                if (existingById.TryGetValue(t.BggGameId, out var row))
                {
                    row.Type = t.Type;
                    row.Name = t.Name;
                    row.ImageUrl = t.ImageUrl;
                    row.ThumbnailUrl = t.ThumbnailUrl;
                    row.MinPlayers = t.MinPlayers;
                    row.MaxPlayers = t.MaxPlayers;
                    row.MinPlayTimeMinutes = t.MinPlayTimeMinutes;
                    row.MaxPlayTimeMinutes = t.MaxPlayTimeMinutes;
                    row.RecommendedPlayerCountsJson = t.RecommendedPlayerCounts.Count == 0
                        ? null
                        : JsonSerializer.Serialize(t.RecommendedPlayerCounts, Json);
                    row.LastFetchedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    var fresh = new BggGame
                    {
                        BggGameId = t.BggGameId,
                        Type = t.Type,
                        Name = t.Name,
                        ImageUrl = t.ImageUrl,
                        ThumbnailUrl = t.ThumbnailUrl,
                        MinPlayers = t.MinPlayers,
                        MaxPlayers = t.MaxPlayers,
                        MinPlayTimeMinutes = t.MinPlayTimeMinutes,
                        MaxPlayTimeMinutes = t.MaxPlayTimeMinutes,
                        RecommendedPlayerCountsJson = t.RecommendedPlayerCounts.Count == 0
                            ? null
                            : JsonSerializer.Serialize(t.RecommendedPlayerCounts, Json),
                    };
                    _db.BggGames.Add(fresh);
                    existingById[t.BggGameId] = fresh;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        // Only "boardgame" items become BggImportItems — expansions and
        // accessories are cached so we don't re-fetch them, but they don't
        // count as candidates to swipe on.
        foreach (var id in gameIds.Distinct())
        {
            if (!existingById.TryGetValue(id, out var row)) continue;
            if (!string.Equals(row.Type, "boardgame", StringComparison.Ordinal)) continue;

            _db.BggImportItems.Add(new BggImportItem
            {
                BggImportId = import.Id,
                BggGameId = id,
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}
