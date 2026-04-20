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
        var import = await _db.BggImports
            .Include(i => i.Items)
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

    /// <summary>Core upsert: fetches/refreshes BggGame rows for <paramref name="gameIds"/>,
    /// replaces the import's items list, and bumps LastRefreshedAt.</summary>
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

        // Upsert the BggGame cache for every ID we don't already know about, or
        // that hasn't been fetched yet. "Forever cache" per the design — only
        // refreshes when we've never seen the ID (avoids slamming BGG on every
        // refresh of an import whose game list is unchanged).
        var known = await _db.BggGames
            .Where(g => gameIds.Contains(g.BggGameId))
            .Select(g => g.BggGameId)
            .ToListAsync(ct);

        var missing = gameIds.Except(known).ToList();
        if (missing.Count > 0)
        {
            var things = await _bgg.FetchThingsAsync(missing, ct);
            foreach (var t in things)
            {
                _db.BggGames.Add(new BggGame
                {
                    BggGameId = t.BggGameId,
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
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Attach items in a second save — need the BggGames to exist first.
        foreach (var id in gameIds)
        {
            _db.BggImportItems.Add(new BggImportItem
            {
                BggImportId = import.Id,
                BggGameId = id,
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}
