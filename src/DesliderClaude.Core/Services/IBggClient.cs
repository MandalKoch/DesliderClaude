using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

/// <summary>Wraps BGG's XML API v2 — the three endpoints we actually need.
/// All calls hit the network; callers own their own caching (we persist via
/// <see cref="IBggImportService"/>).</summary>
public interface IBggClient
{
    Task<BggGeekListFetch> FetchGeekListAsync(int geekListId, CancellationToken ct = default);
    Task<BggCollectionFetch> FetchCollectionAsync(string username, CancellationToken ct = default);
    Task<IReadOnlyList<BggThingFetch>> FetchThingsAsync(IReadOnlyList<int> bggGameIds, CancellationToken ct = default);
}
