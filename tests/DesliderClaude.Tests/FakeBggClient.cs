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
    public Dictionary<string, IReadOnlyList<int>> Searches { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IReadOnlyList<int>> SearchResults => Searches;
    public Dictionary<string, Exception> SearchFailures { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Task> SearchDelays { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SearchCalls { get; } = new();
    public List<IReadOnlyList<int>> FetchThingsCalls { get; } = new();

    public async Task<IReadOnlyList<int>> SearchAsync(string query, CancellationToken ct = default)
    {
        SearchCalls.Add(query);
        if (SearchDelays.TryGetValue(query, out var delay)) await delay.WaitAsync(ct);
        if (SearchFailures.TryGetValue(query, out var error)) throw error;
        return Searches.TryGetValue(query, out var ids) ? ids : Array.Empty<int>();
    }

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
        FetchThingsCalls.Add(bggGameIds.ToArray());
        var result = bggGameIds
            .Where(id => Things.ContainsKey(id))
            .Select(id => Things[id])
            .ToList();
        return Task.FromResult<IReadOnlyList<BggThingFetch>>(result);
    }
}
