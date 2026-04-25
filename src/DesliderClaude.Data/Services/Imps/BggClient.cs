using System.Globalization;
using System.Net;
using System.Xml.Linq;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;

namespace DesliderClaude.Data.Services.Imps;

/// <summary>BGG XML API v2 client. Handles the three quirks we care about:
/// (1) collection endpoint returns HTTP 202 while the result is queued — we
/// retry with a short delay; (2) /thing caps each request at 20 IDs, so we
/// chunk and concatenate; (3) the suggested_numplayers poll is
/// nested <c>&lt;results numplayers="N"&gt;</c> with three child <c>&lt;result&gt;</c>s —
/// we translate the tallies into <see cref="BggPlayerCountKind"/> using the
/// standard "Best+Recommended &gt; Not Recommended" rule.</summary>
internal sealed class BggClient : IBggClient
{
    private static readonly TimeSpan CollectionRetryDelay = TimeSpan.FromSeconds(2);
    private const int CollectionRetryLimit = 5;
    private const int ThingBatchSize = 20;

    private readonly HttpClient _http;

    public BggClient(HttpClient http) => _http = http;

    public async Task<BggGeekListFetch> FetchGeekListAsync(int geekListId, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"geeklist/{geekListId}", ct);
        await EnsureSuccessAsync(resp, ct);

        var doc = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.Root ?? throw new InvalidOperationException("Empty geeklist response.");

        var name = (string?)root.Element("title") ?? $"Geeklist {geekListId}";
        var gameIds = root.Elements("item")
            .Where(e => (string?)e.Attribute("objecttype") == "thing")
            .Select(e => int.TryParse((string?)e.Attribute("objectid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        return new BggGeekListFetch(geekListId, name.Trim(), gameIds);
    }

    public async Task<BggCollectionFetch> FetchCollectionAsync(string username, CancellationToken ct = default)
    {
        var path = $"collection?username={Uri.EscapeDataString(username)}&own=1";
        for (var attempt = 0; attempt < CollectionRetryLimit; attempt++)
        {
            using var resp = await _http.GetAsync(path, ct);
            if (resp.StatusCode == HttpStatusCode.Accepted)
            {
                await Task.Delay(CollectionRetryDelay, ct);
                continue;
            }
            await EnsureSuccessAsync(resp, ct);

            var doc = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.Root ?? throw new InvalidOperationException("Empty collection response.");

            var gameIds = root.Elements("item")
                .Select(e => int.TryParse((string?)e.Attribute("objectid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            return new BggCollectionFetch(username, gameIds);
        }

        throw new InvalidOperationException($"BGG never materialised collection for '{username}' after {CollectionRetryLimit} attempts.");
    }

    public async Task<IReadOnlyList<BggThingFetch>> FetchThingsAsync(IReadOnlyList<int> bggGameIds, CancellationToken ct = default)
    {
        if (bggGameIds.Count == 0) return Array.Empty<BggThingFetch>();

        var things = new List<BggThingFetch>();
        foreach (var batch in bggGameIds.Distinct().Chunk(ThingBatchSize))
        {
            var ids = string.Join(',', batch);
            using var resp = await _http.GetAsync($"thing?id={ids}&stats=1", ct);
            await EnsureSuccessAsync(resp, ct);

            var doc = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.Root ?? throw new InvalidOperationException("Empty thing response.");

            foreach (var item in root.Elements("item"))
            {
                if (!int.TryParse((string?)item.Attribute("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
                    continue;

                var primary = item.Elements("name").FirstOrDefault(n => (string?)n.Attribute("type") == "primary");
                var name = (string?)primary?.Attribute("value") ?? (string?)item.Element("name")?.Attribute("value") ?? $"BGG {id}";

                things.Add(new BggThingFetch(
                    BggGameId: id,
                    Type: (string?)item.Attribute("type") ?? string.Empty,
                    Name: name,
                    ImageUrl: (string?)item.Element("image"),
                    ThumbnailUrl: (string?)item.Element("thumbnail"),
                    MinPlayers: TryReadValue(item.Element("minplayers")),
                    MaxPlayers: TryReadValue(item.Element("maxplayers")),
                    MinPlayTimeMinutes: TryReadValue(item.Element("minplaytime")),
                    MaxPlayTimeMinutes: TryReadValue(item.Element("maxplaytime")),
                    RecommendedPlayerCounts: ParsePlayerCountPoll(item)));
            }
        }

        return things;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        var snippet = body.Length > 500 ? body[..500] + "…" : body;
        throw new HttpRequestException(
            $"BGG {(int)resp.StatusCode} {resp.ReasonPhrase} for {resp.RequestMessage?.RequestUri}: {snippet}",
            inner: null,
            statusCode: resp.StatusCode);
    }

    private static int? TryReadValue(XElement? el)
    {
        if (el is null) return null;
        var v = (string?)el.Attribute("value");
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;
    }

    internal static IReadOnlyList<BggPlayerCountVote> ParsePlayerCountPoll(XElement item)
    {
        var poll = item.Elements("poll").FirstOrDefault(p => (string?)p.Attribute("name") == "suggested_numplayers");
        if (poll is null) return Array.Empty<BggPlayerCountVote>();

        var votes = new List<BggPlayerCountVote>();
        foreach (var results in poll.Elements("results"))
        {
            var numPlayersAttr = (string?)results.Attribute("numplayers");
            if (string.IsNullOrEmpty(numPlayersAttr)) continue;
            // Skip "N+" bucket — rare, and we'd need a different schema to represent.
            if (!int.TryParse(numPlayersAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numPlayers)) continue;
            if (numPlayers <= 0) continue;

            int best = 0, rec = 0, not = 0;
            foreach (var r in results.Elements("result"))
            {
                var value = (string?)r.Attribute("value");
                if (!int.TryParse((string?)r.Attribute("numvotes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var votesCount)) continue;
                switch (value)
                {
                    case "Best": best = votesCount; break;
                    case "Recommended": rec = votesCount; break;
                    case "Not Recommended": not = votesCount; break;
                }
            }

            if (best + rec + not == 0) continue;

            BggPlayerCountKind kind;
            if (best > rec && best > not) kind = BggPlayerCountKind.Best;
            else if (best + rec > not) kind = BggPlayerCountKind.Recommended;
            else kind = BggPlayerCountKind.NotRecommended;

            votes.Add(new BggPlayerCountVote(numPlayers, kind));
        }

        return votes;
    }
}
