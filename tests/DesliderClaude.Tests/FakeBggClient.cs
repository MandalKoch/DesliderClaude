using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;

namespace DesliderClaude.Tests;

/// <summary>Stand-in for <see cref="IBggClient"/> that serves predictable data
/// from in-memory tables — keeps tests hermetic (BGG's real API is slow and
/// rate-limits hard).</summary>
public sealed class FakeBggClient : IBggClient
{
    public Dictionary<int, BggGeekListFetch> GeekLists { get; } = new();
    public Dictionary<string, BggCollectionFetch> Collections { get; } = new();
    public Dictionary<int, BggThingFetch> Things { get; } = new();

    public Task<BggGeekListFetch> FetchGeekListAsync(int geekListId, CancellationToken ct = default)
    {
        if (!GeekLists.TryGetValue(geekListId, out var list))
            throw new InvalidOperationException($"FakeBggClient has no geeklist {geekListId}.");
        return Task.FromResult(list);
    }

    public Task<BggCollectionFetch> FetchCollectionAsync(string username, CancellationToken ct = default)
    {
        if (!Collections.TryGetValue(username, out var col))
            throw new InvalidOperationException($"FakeBggClient has no collection for '{username}'.");
        return Task.FromResult(col);
    }

    public Task<IReadOnlyList<BggThingFetch>> FetchThingsAsync(IReadOnlyList<int> bggGameIds, CancellationToken ct = default)
    {
        var result = bggGameIds
            .Where(id => Things.ContainsKey(id))
            .Select(id => Things[id])
            .ToList();
        return Task.FromResult<IReadOnlyList<BggThingFetch>>(result);
    }
}
