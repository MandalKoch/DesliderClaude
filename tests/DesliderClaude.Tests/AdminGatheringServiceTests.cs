using System.Text.Json;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using DesliderClaude.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[TestFixture]
public sealed class AdminGatheringServiceTests
{
    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private DesliderClaudeDbContext _db = null!;
    private IAdminService _admin = null!;
    private IGatheringService _gatherings = null!;
    private IGameNightService _nights = null!;
    private IVotingService _voting = null!;
    private Guid _host;
    private Guid _account;

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
        _admin = _scope.ServiceProvider.GetRequiredService<IAdminService>();
        _gatherings = _scope.ServiceProvider.GetRequiredService<IGatheringService>();
        _nights = _scope.ServiceProvider.GetRequiredService<IGameNightService>();
        _voting = _scope.ServiceProvider.GetRequiredService<IVotingService>();
        var host = new User { Username = "host" };
        var account = new User { Username = "account" };
        _host = host.Id;
        _account = account.Id;
        _db.Users.AddRange(host, account);
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
    public async Task Gatherings_have_separate_stats_and_scoped_token_free_participant_choices()
    {
        const string guestToken = "private-gathering-guest-credential";
        const string accountToken = "private-gathering-account-credential";
        var gathering = await _gatherings.CreateAsync("Cabin weekend", _host);
        var guest = await _gatherings.JoinAsync(gathering.Id, "Guest", guestToken);
        await _gatherings.JoinAsync(gathering.Id, "Account member", accountToken, _account);
        foreach (var name in new[] { "Azul", "Cascadia", "Dune" })
            await _gatherings.AddGameAsync(gathering.Id, guestToken, new GameCandidate(name));
        var games = (await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games;
        await _gatherings.SetInterestAsync(gathering.Id, guestToken, games[0].Id, true);
        await _gatherings.SetInterestAsync(gathering.Id, guestToken, games[1].Id, false);
        await _gatherings.SetInterestAsync(gathering.Id, accountToken, games[0].Id, false, _account);
        var round = await _gatherings.StartVoteAsync(gathering.Id, _host, "Legacy round", [games[0].Id, games[1].Id]);
        await _gatherings.MarkPlayedAsync(gathering.Id, _host, games[0].Id);

        var other = await _gatherings.CreateAsync("Another gathering", _host);
        await _gatherings.JoinAsync(other.Id, "Other member", "other-private-credential");
        await _gatherings.AddGameAsync(other.Id, "other-private-credential", new GameCandidate("Other game"));
        var otherGame = (await _gatherings.GetByShareCodeAsync(other.ShareCode))!.Games.Single();
        await _gatherings.SetInterestAsync(other.Id, "other-private-credential", otherGame.Id, true);
        await _gatherings.SetVotingClosedAsync(other.Id, _host, true);

        var overview = await _admin.GetOverviewAsync();
        var rows = await _admin.ListGatheringsAsync();
        var detail = (await _admin.GetGatheringDetailAsync(gathering.ShareCode))!;
        var guestChoices = detail.Participants.Single(p => p.Id == guest.Id);
        var accountChoices = detail.Participants.Single(p => p.Username == "account");
        Assert.Multiple(() =>
        {
            Assert.That(overview.GatheringCount, Is.EqualTo(2));
            Assert.That(overview.OpenGatheringCount, Is.EqualTo(1));
            Assert.That(overview.ClosedGatheringCount, Is.EqualTo(1));
            Assert.That(overview.GatheringMemberCount, Is.EqualTo(3));
            Assert.That(overview.GatheringGameCount, Is.EqualTo(4));
            Assert.That(overview.GatheringVoteCount, Is.EqualTo(4));
            Assert.That(overview.NightCount, Is.EqualTo(1), "Only the legacy round is a game night.");
            Assert.That(overview.SwipeCount, Is.Zero, "Gathering interest is separate from night swipes.");
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(detail.Gathering.HostUsername, Is.EqualTo("host"));
            Assert.That(detail.Gathering.IsVotingClosed, Is.False);
            Assert.That(detail.Gathering.MemberCount, Is.EqualTo(2));
            Assert.That(detail.Gathering.GameCount, Is.EqualTo(3));
            Assert.That(detail.Gathering.VoteCount, Is.EqualTo(3));
            Assert.That(detail.Participants, Has.Count.EqualTo(2));
            Assert.That(guestChoices.Username, Is.Null);
            Assert.That(guestChoices.Choices.Select(c => c.Yes), Is.EqualTo(new bool?[] { true, false, null }));
            Assert.That(accountChoices.Choices.Select(c => c.Yes), Is.EqualTo(new bool?[] { false, null, null }));
            Assert.That(detail.Games.Select(g => g.BringerDisplayName), Is.All.EqualTo("Guest"));
            Assert.That(detail.Rounds.Single().Id, Is.EqualTo(round.Id));
            Assert.That(detail.PlayedGames.Single().GameName, Is.EqualTo(games[0].Name));
            Assert.That(detail.Games.Any(g => g.Id == otherGame.Id), Is.False);
            Assert.That(JsonSerializer.Serialize(detail), Does.Not.Contain(guestToken).And.Not.Contain(accountToken));
        });
        Assert.That(await _admin.GetGatheringDetailAsync("missing-gathering"), Is.Null);
    }

    [Test]
    public async Task Night_admin_choices_include_play_pass_and_not_voted_without_other_nights_or_credentials()
    {
        const string guestToken = "private-night-guest-credential";
        const string accountToken = "private-night-account-credential";
        var night = await _nights.CreateAsync("Friday", null,
            [new GameCandidate("Azul"), new GameCandidate("Cascadia"), new GameCandidate("Dune")], _host);
        var guest = await _voting.JoinAsync(night.Id, "Guest", guestToken);
        await _voting.JoinAsync(night.Id, "Account member", accountToken, _account);
        var games = night.Games.OrderBy(g => g.Name).ToList();
        await _voting.RecordSwipeAsync(guestToken, games[0].Id, true);
        await _voting.RecordSwipeAsync(guestToken, games[1].Id, false);
        await _voting.RecordSwipeAsync(accountToken, games[0].Id, false, _account);
        var other = await _nights.CreateAsync("Elsewhere", null, [new GameCandidate("Other game")], _host);
        await _voting.JoinAsync(other.Id, "Other person", "other-private-voter");
        await _voting.RecordSwipeAsync("other-private-voter", other.Games.Single().Id, true);

        var voters = await _admin.ListVotersForNightAsync(night.Id);
        var guestRow = voters.Single(v => v.Id == guest.Id);
        var accountRow = voters.Single(v => v.Username == "account");
        Assert.Multiple(() =>
        {
            Assert.That(voters, Has.Count.EqualTo(2));
            Assert.That(guestRow.Username, Is.Null);
            Assert.That(guestRow.SwipeCount, Is.EqualTo(2));
            Assert.That(guestRow.YesCount, Is.EqualTo(1));
            Assert.That(guestRow.NoCount, Is.EqualTo(1));
            Assert.That(guestRow.Choices.Select(c => c.Yes), Is.EqualTo(new bool?[] { true, false, null }));
            Assert.That(accountRow.Choices.Select(c => c.Yes), Is.EqualTo(new bool?[] { false, null, null }));
            Assert.That(guestRow.Choices.Select(c => c.GameId), Is.EquivalentTo(games.Select(g => g.Id)));
            Assert.That(JsonSerializer.Serialize(voters), Does.Not.Contain(guestToken).And.Not.Contain(accountToken));
        });
        Assert.That(await _admin.ListVotersForNightAsync(Guid.CreateVersion7()), Is.Empty);
    }
}
