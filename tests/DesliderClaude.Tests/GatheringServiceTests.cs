using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using DesliderClaude.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[TestFixture]
public class GatheringServiceTests
{
    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private DesliderClaudeDbContext _db = null!;
    private IGatheringService _service = null!;
    private Guid _host;

    [SetUp]
    public async Task SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDesliderData("Data Source=:memory:");
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<DesliderClaudeDbContext>();
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
        _service = _scope.ServiceProvider.GetRequiredService<IGatheringService>();
        var host = new User { Username = "host" };
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
    public async Task Gathering_PreservesBringListAcrossIndependentVotesAndRepeatedPlays()
    {
        var gathering = await _service.CreateAsync("Cabin weekend", _host);
        var member = await _service.JoinAsync(gathering.Id, "Alex", "alex-session");
        await _service.AddGameAsync(gathering.Id, "alex-session", Candidate("Cascadia"));
        await _service.AddGameAsync(gathering.Id, "alex-session", Candidate("Dune"));
        gathering = (await _service.GetByShareCodeAsync(gathering.ShareCode))!;
        var ids = gathering.Games.Select(x => x.Id).ToArray();
        foreach (var id in ids)
            await _service.ConfirmGameAsync(gathering.Id, _host, id, true, member.Id);
        await _service.SetInterestAsync(gathering.Id, "alex-session", ids[0], true);
        await _service.SetInterestAsync(gathering.Id, "alex-session", ids[0], false);

        var first = await _service.StartVoteAsync(gathering.Id, _host, "Friday evening", ids);
        var second = await _service.StartVoteAsync(gathering.Id, _host, "Saturday morning", ids);
        await _service.MarkPlayedAsync(gathering.Id, _host, ids[0]);
        await _service.MarkPlayedAsync(gathering.Id, _host, ids[0]);
        gathering = (await _service.GetByShareCodeAsync(gathering.ShareCode))!;

        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.Games.Select(x => x.GatheringGameId), Is.EquivalentTo(ids));
            Assert.That(first.TargetDate, Is.Null);
            Assert.That(gathering.Rounds, Has.Count.EqualTo(2));
            Assert.That(gathering.PlayedGames, Has.Count.EqualTo(2));
            Assert.That(gathering.Games.All(x => x.IsConfirmed && x.BringerMemberId == member.Id), Is.True);
            Assert.That(gathering.Games.Single(x => x.Id == ids[0]).Interests.Single().Interested, Is.False);
            Assert.That(first.IsClosed, Is.False, "Recording play must not silently close a vote.");
        });
    }

    [Test]
    public async Task Membership_IsScopedAndLinkedAccountsRejoinWithoutDuplicates()
    {
        var one = await _service.CreateAsync("One", _host);
        var two = await _service.CreateAsync("Two", _host);
        var first = await _service.JoinAsync(one.Id, "Host", "first-device", _host);
        var same = await _service.JoinAsync(one.Id, "Host", "first-device", _host);
        var second = await _service.JoinAsync(one.Id, "Host", "second-device", _host);

        Assert.That(same.Id, Is.EqualTo(first.Id));
        Assert.That(second.Id, Is.EqualTo(first.Id));
        Assert.That(await _service.GetMemberAsync(one.Id, "second-device"), Is.Not.Null);
        Assert.That(await _service.GetMemberAsync(two.Id, "second-device"), Is.Null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.AddGameAsync(two.Id, "second-device", Candidate("Dune")));
        var listed = await _service.ListForUserAsync(_host);
        Assert.That(listed.Single(x => x.Id == one.Id).MemberCount, Is.EqualTo(1));
    }

    [Test]
    public async Task HostActions_RejectOtherUsersAndCrossGatheringIds()
    {
        var one = await _service.CreateAsync("One", _host);
        var two = await _service.CreateAsync("Two", _host);
        var member = await _service.JoinAsync(one.Id, "Alex", "one-session");
        var other = await _service.JoinAsync(two.Id, "Bea", "two-session");
        await _service.AddGameAsync(one.Id, "one-session", Candidate("Dune"));
        var game = (await _service.GetByShareCodeAsync(one.ShareCode))!.Games.Single();
        var stranger = Guid.CreateVersion7();

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ConfirmGameAsync(one.Id, stranger, game.Id, true, member.Id));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.StartVoteAsync(one.Id, stranger, "Vote", [game.Id]));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.MarkPlayedAsync(one.Id, stranger, game.Id));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.ConfirmGameAsync(one.Id, _host, game.Id, true, other.Id));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.ConfirmGameAsync(two.Id, _host, game.Id, true, other.Id));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.SetInterestAsync(two.Id, "two-session", game.Id, true));
    }

    [Test]
    public async Task Votes_RequireTwoDistinctConfirmedGamesFromThisGathering()
    {
        var gathering = await _service.CreateAsync("Weekend", _host);
        await _service.JoinAsync(gathering.Id, "Host", "host-session", _host);
        await _service.AddGameAsync(gathering.Id, "host-session", Candidate("Dune"));
        await _service.AddGameAsync(gathering.Id, "host-session", Candidate("Cascadia"));
        var games = (await _service.GetByShareCodeAsync(gathering.ShareCode))!.Games;
        var ids = games.Select(x => x.Id).ToArray();
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartVoteAsync(gathering.Id, _host, "Vote", ids));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.MarkPlayedAsync(gathering.Id, _host, ids[0]));
        await _service.ConfirmGameAsync(gathering.Id, _host, ids[0], true, null);
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartVoteAsync(gathering.Id, _host, "Vote", [ids[0], ids[0]]));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartVoteAsync(gathering.Id, _host, "Vote", [ids[0], Guid.CreateVersion7()]));
    }

    [Test]
    public async Task Suggestions_RejectDuplicateNamesAndInvalidInput()
    {
        var gathering = await _service.CreateAsync("Weekend", _host);
        await _service.JoinAsync(gathering.Id, "Alex", "alex-session");
        await _service.AddGameAsync(gathering.Id, "alex-session", Candidate("Dune"));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddGameAsync(gathering.Id, "alex-session", Candidate(" dune ")));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.AddGameAsync(gathering.Id, "alex-session", Candidate(" ")));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(new string('x', 201), _host));
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.JoinAsync(gathering.Id, new string('x', 41), "another-session"));
    }

    private static GameCandidate Candidate(string name) => new(name, null, null, null);
}
