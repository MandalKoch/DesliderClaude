using System.Net;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using DesliderClaude.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[TestFixture]
public sealed class PickerBackendTests
{
    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private DesliderClaudeDbContext _db = null!;
    private IBggImportService _bgg = null!;
    private IGatheringService _gatherings = null!;
    private FakeBggClient _fake = null!;
    private Guid _host;

    [SetUp]
    public async Task SetUp()
    {
        _fake = new FakeBggClient();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDesliderData("Data Source=:memory:");
        services.RemoveAll<IBggClient>();
        services.AddSingleton<IBggClient>(_fake);
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<DesliderClaudeDbContext>();
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
        _bgg = _scope.ServiceProvider.GetRequiredService<IBggImportService>();
        _gatherings = _scope.ServiceProvider.GetRequiredService<IGatheringService>();
        var host = new User { Username = "picker-host" };
        _host = host.Id;
        _db.Users.Add(host);
        await _db.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _db.DisposeAsync();
        _scope.Dispose();
        await _provider.DisposeAsync();
    }

    [Test]
    public async Task Search_BoundsResultsCachesQueryAndDetailsAndExcludesExpansions()
    {
        _fake.Searches["Catan"] = Enumerable.Range(1, 25).ToArray();
        foreach (var id in Enumerable.Range(1, 25))
            _fake.Things[id] = Thing(id, id == 1 ? "boardgameexpansion" : "boardgame");

        var first = await _bgg.SearchGamesAsync(" Catan ");
        var second = await _bgg.SearchGamesAsync("CATAN");
        var selected = await _bgg.GetGameAsync(2);
        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Count.EqualTo(19));
            Assert.That(first.Select(g => g.BggGameId), Is.EqualTo(Enumerable.Range(2, 19)));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(selected.Name, Is.EqualTo("Game 2"));
            Assert.That(selected.MaxPlayers, Is.EqualTo(4));
            Assert.That(_fake.SearchCalls, Is.EqualTo(new[] { "Catan" }));
            Assert.That(_fake.FetchThingsCalls, Has.Count.EqualTo(1));
            Assert.That(_fake.FetchThingsCalls[0], Has.Count.EqualTo(20));
        });
    }

    [Test]
    public void Search_RejectsInvalidQueriesBeforeCallingBgg()
    {
        foreach (var query in new[] { "", " ", "a", new string('x', 201) })
            Assert.ThrowsAsync<InvalidOperationException>(() => _bgg.SearchGamesAsync(query));
        Assert.That(_fake.SearchCalls, Is.Empty);
    }

    [Test]
    public async Task Search_FailuresAreNotCachedAndCanRecover()
    {
        _fake.SearchFailures["Catan"] = new HttpRequestException("BGG unavailable");
        Assert.ThrowsAsync<HttpRequestException>(() => _bgg.SearchGamesAsync("Catan"));
        _fake.SearchFailures.Clear();
        _fake.Searches["Catan"] = [13];
        _fake.Things[13] = Thing(13);
        Assert.That(await _bgg.SearchGamesAsync("Catan"), Has.Count.EqualTo(1));
        Assert.That(_fake.SearchCalls, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SelectedGame_RejectsUnknownInvalidAndExpansionIds()
    {
        _fake.Things[7] = Thing(7, "boardgameexpansion");
        Assert.ThrowsAsync<InvalidOperationException>(() => _bgg.GetGameAsync(0));
        Assert.ThrowsAsync<InvalidOperationException>(() => _bgg.GetGameAsync(-1));
        Assert.ThrowsAsync<InvalidOperationException>(() => _bgg.GetGameAsync(999));
        Assert.ThrowsAsync<InvalidOperationException>(() => _bgg.GetGameAsync(7));
        Assert.That(await _db.BggGames.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task BggClient_SearchEncodesQueryAndAcceptsOnlyValidBoardgameIds()
    {
        using var handler = new SearchHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDesliderData("Data Source=:memory:");
        services.AddHttpClient(nameof(IBggClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IBggClient>();
        var ids = await client.SearchAsync("A&B / C?");
        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestUri!.Query, Does.Contain("query=A%26B%20%2F%20C%3F"));
            Assert.That(handler.RequestUri.Query, Does.Contain("type=boardgame"));
            Assert.That(ids, Is.EqualTo(new[] { 13, 42 }));
        });
    }

    [Test]
    public async Task SuggestionRemoval_RequiresScopedMembershipAndOwnerOrHostAndDeletesInterest()
    {
        var gathering = await _gatherings.CreateAsync("Weekend", _host);
        await _gatherings.JoinAsync(gathering.Id, "Alex", "alex");
        await _gatherings.JoinAsync(gathering.Id, "Bea", "bea");
        await _gatherings.JoinAsync(gathering.Id, "Host", "host", _host);
        await _gatherings.AddGameAsync(gathering.Id, "alex", new GameCandidate("Azul"));
        var game = (await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games.Single();
        await _gatherings.SetInterestAsync(gathering.Id, "bea", game.Id, true);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.RemoveGameAsync(gathering.Id, "missing", game.Id, _host));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.RemoveGameAsync(gathering.Id, "bea", game.Id));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.RemoveGameAsync(gathering.Id, "bea", game.Id, Guid.CreateVersion7()));
        await _gatherings.RemoveGameAsync(gathering.Id, "host", game.Id, _host);
        Assert.That((await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games, Is.Empty);
        Assert.That(await _db.GatheringInterests.CountAsync(), Is.Zero);
        await _gatherings.AddGameAsync(gathering.Id, "alex", new GameCandidate("Azul"));
        game = (await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games.Single();
        await _gatherings.RemoveGameAsync(gathering.Id, "alex", game.Id);
        Assert.That((await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games, Is.Empty);
    }

    [Test]
    public async Task SuggestionRemoval_PreservesVotesAndPlayedHistoryAndRejectsCrossIds()
    {
        var gathering = await _gatherings.CreateAsync("Weekend", _host);
        var other = await _gatherings.CreateAsync("Other", _host);
        await _gatherings.JoinAsync(gathering.Id, "Host", "host", _host);
        await _gatherings.JoinAsync(other.Id, "Host", "other", _host);
        foreach (var name in new[] { "Azul", "Dune" })
            await _gatherings.AddGameAsync(gathering.Id, "host", new GameCandidate(name));
        var ids = (await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games.Select(g => g.Id).ToArray();
        Assert.ThrowsAsync<InvalidOperationException>(() => _gatherings.RemoveGameAsync(other.Id, "other", ids[0], _host));
        foreach (var id in ids)
            await _gatherings.ConfirmGameAsync(gathering.Id, _host, id, true, null);
        var round = await _gatherings.StartVoteAsync(gathering.Id, _host, "Next?", ids);
        await _gatherings.MarkPlayedAsync(gathering.Id, _host, ids[0]);
        foreach (var id in ids)
            await _gatherings.ConfirmGameAsync(gathering.Id, _host, id, false, null);
        Assert.ThrowsAsync<InvalidOperationException>(() => _gatherings.RemoveGameAsync(gathering.Id, "host", ids[1], _host));
        Assert.That((await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games.All(g => g.IsUsedInVote), Is.True);
        _db.GameNights.Remove(round);
        await _db.SaveChangesAsync();
        Assert.ThrowsAsync<InvalidOperationException>(() => _gatherings.RemoveGameAsync(gathering.Id, "host", ids[0], _host));
        await _gatherings.RemoveGameAsync(gathering.Id, "host", ids[1], _host);
        Assert.That((await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.PlayedGames, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task BggSuggestions_UseAuthoritativeCachedMetadataAndRejectExpansions()
    {
        _fake.Things[13] = Thing(13);
        _fake.Things[14] = Thing(14, "boardgameexpansion");
        await _bgg.GetGameAsync(13);
        Assert.ThrowsAsync<InvalidOperationException>(() => _bgg.GetGameAsync(14));
        var gathering = await _gatherings.CreateAsync("Weekend", _host);
        await _gatherings.JoinAsync(gathering.Id, "Alex", "alex");
        await _gatherings.AddGameAsync(gathering.Id, "alex", new GameCandidate("Forged", 13, "https://example.com/forged.jpg"));
        var game = (await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games.Single();
        Assert.Multiple(() =>
        {
            Assert.That(game.Name, Is.EqualTo("Game 13"));
            Assert.That(game.ImageUrl, Is.EqualTo("https://example.com/game.jpg"));
        });
        Assert.ThrowsAsync<InvalidOperationException>(() => _gatherings.AddGameAsync(gathering.Id, "alex", new GameCandidate("Expansion", 14)));
    }

    private static BggThingFetch Thing(int id, string type = "boardgame")
        => new(id, type, $"Game {id}", "https://example.com/game.jpg", "https://example.com/thumb.jpg", 2, 4, 30, 60, Array.Empty<BggPlayerCountVote>());

    private sealed class SearchHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <items>
                      <item type="boardgame" id="13"/><item type="boardgame" id="13"/>
                      <item type="boardgameexpansion" id="14"/><item type="boardgame" id="bad"/>
                      <item type="boardgame" id="0"/><item type="boardgame" id="42"/>
                    </items>
                    """)
            });
        }
    }
}
