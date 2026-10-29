using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using DesliderClaude.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[TestFixture]
public sealed class GuestIdentityTests
{
    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private DesliderClaudeDbContext _db = null!;
    private IVotingService _voting = null!;
    private IGatheringService _gatherings = null!;
    private IGameNightService _nights = null!;
    private Guid _account;
    private Guid _otherAccount;

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
        _voting = _scope.ServiceProvider.GetRequiredService<IVotingService>();
        _gatherings = _scope.ServiceProvider.GetRequiredService<IGatheringService>();
        _nights = _scope.ServiceProvider.GetRequiredService<IGameNightService>();
        var account = new User { Username = "alex" };
        var other = new User { Username = "bea" };
        _account = account.Id;
        _otherAccount = other.Id;
        _db.Users.AddRange(account, other);
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
    public async Task Night_guest_rejoin_and_case_insensitive_name_reuse_preserve_votes()
    {
        var night = await NightAsync();
        var guest = await _voting.JoinAsync(night.Id, " Alex ", "first");
        await _voting.RecordSwipeAsync("first", night.Games.First().Id, true);
        var same = await _voting.JoinAsync(night.Id, "alex", "second");
        Assert.That(same.Id, Is.EqualTo(guest.Id));
        Assert.That(await _voting.GetProgressAsync("first"), Is.Null);
        await _voting.RejoinGuestAsync(night.Id, guest.Id, "third");
        Assert.That((await _voting.GetProgressAsync("third"))!.Swipes[night.Games.First().Id], Is.True);
        Assert.That(await _voting.GetVoterCountAsync(night.Id), Is.EqualTo(1));
        var other = await NightAsync();
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.RejoinGuestAsync(other.Id, guest.Id, "cross"));
        Assert.That(await _voting.ListParticipantsAsync(night.Id), Is.EqualTo(new[] { new ParticipantIdentity(guest.Id, "Alex", false) }));
    }

    [Test]
    public async Task Night_account_claim_requires_explicit_guest_selection_and_owner_for_every_token_action()
    {
        var night = await NightAsync();
        var gameId = night.Games.First().Id;
        var guest = await _voting.JoinAsync(night.Id, "Alex", "guest");
        await _voting.RecordSwipeAsync("guest", gameId, true);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _voting.JoinAsync(night.Id, "Alex", "guest", _account));
        await _voting.RejoinGuestAsync(night.Id, guest.Id, "protected", _account);
        Assert.That(await _voting.GetProgressAsync("protected"), Is.Null);
        Assert.That(await _voting.GetProgressAsync("protected", _otherAccount), Is.Null);
        Assert.That(await _voting.PickNextGameAsync("protected"), Is.Null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _voting.RecordSwipeAsync("protected", gameId, false));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _voting.RemoveSwipeAsync("protected", gameId, _otherAccount));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _voting.JoinAsync(night.Id, "Alex", "protected", _otherAccount));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _voting.RejoinGuestAsync(night.Id, guest.Id, "stolen"));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _voting.JoinAsync(night.Id, "alex", "name-takeover"));
        var returned = await _voting.JoinAsync(night.Id, "Alex", "new-device", _account);
        Assert.That(returned.Id, Is.EqualTo(guest.Id));
        Assert.That((await _voting.GetProgressAsync("new-device", _account))!.Swipes[gameId], Is.True);
        await _voting.RemoveSwipeAsync("new-device", gameId, _account);
        var anotherGuest = await _voting.JoinAsync(night.Id, "Another guest", "another");
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.RejoinGuestAsync(night.Id, anotherGuest.Id, "duplicate-account", _account));
    }

    [Test]
    public async Task Gathering_guest_rejoin_preserves_games_and_interest_then_account_protects_identity()
    {
        var gathering = await _gatherings.CreateAsync("Weekend", _otherAccount);
        var guest = await _gatherings.JoinAsync(gathering.Id, " Alex ", "first");
        await _gatherings.AddGameAsync(gathering.Id, "first", new GameCandidate("Azul"));
        var game = (await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games.Single();
        await _gatherings.SetInterestAsync(gathering.Id, "first", game.Id, true);
        var same = await _gatherings.JoinAsync(gathering.Id, "alex", "second");
        Assert.That(same.Id, Is.EqualTo(guest.Id));
        Assert.That(await _gatherings.GetMemberAsync(gathering.Id, "first"), Is.Null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.JoinAsync(gathering.Id, "Alex", "second", _account));
        await _gatherings.RejoinGuestAsync(gathering.Id, guest.Id, "protected", _account);
        Assert.That(await _gatherings.GetMemberAsync(gathering.Id, "protected"), Is.Null);
        Assert.That(await _gatherings.GetMemberAsync(gathering.Id, "protected", _otherAccount), Is.Null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.AddGameAsync(gathering.Id, "protected", new GameCandidate("Dune")));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.SetInterestAsync(gathering.Id, "protected", game.Id, false, _otherAccount));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.RemoveGameAsync(gathering.Id, "protected", game.Id));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.RejoinGuestAsync(gathering.Id, guest.Id, "stolen"));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.JoinAsync(gathering.Id, "alex", "name-takeover"));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _gatherings.SetVotingClosedAsync(gathering.Id, _account, true));
        var returned = await _gatherings.JoinAsync(gathering.Id, "Alex", "new-device", _account);
        Assert.That(returned.Id, Is.EqualTo(guest.Id));
        gathering = (await _gatherings.GetByShareCodeAsync(gathering.ShareCode))!;
        Assert.That(gathering.Members, Has.Count.EqualTo(1));
        Assert.That(gathering.Games.Single().Interests.Single().Interested, Is.True);
        await _gatherings.SetInterestAsync(gathering.Id, "new-device", game.Id, false, _account);
        var anotherGuest = await _gatherings.JoinAsync(gathering.Id, "Another guest", "another");
        Assert.ThrowsAsync<InvalidOperationException>(() => _gatherings.RejoinGuestAsync(gathering.Id, anotherGuest.Id, "duplicate-account", _account));
        var other = await _gatherings.CreateAsync("Elsewhere", _otherAccount);
        Assert.ThrowsAsync<InvalidOperationException>(() => _gatherings.RejoinGuestAsync(other.Id, guest.Id, "cross"));
    }

    [Test]
    public async Task Ambiguous_legacy_names_require_explicit_selection_instead_of_merging_people()
    {
        var night = await NightAsync();
        var gathering = await _gatherings.CreateAsync("Weekend", _account);
        foreach (var token in new[] { "one", "two" })
        {
            _db.Voters.Add(new Voter { GameNightId = night.Id, DisplayName = "Alex", VoterToken = token });
            _db.GatheringMembers.Add(new GatheringMember { GatheringId = gathering.Id, DisplayName = "Alex", Token = token });
        }
        await _db.SaveChangesAsync();
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.JoinAsync(night.Id, "alex", "new"));
        Assert.ThrowsAsync<InvalidOperationException>(() => _gatherings.JoinAsync(gathering.Id, "alex", "new"));
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.JoinAsync(night.Id, new string('x', 41), "long"));
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.JoinAsync(night.Id, " ", "empty"));
    }

    [Test]
    public async Task Night_writes_reject_cross_night_games_and_closed_votes()
    {
        var night = await NightAsync();
        var other = await NightAsync();
        await _voting.JoinAsync(night.Id, "Alex", "guest");
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.RecordSwipeAsync("guest", other.Games.First().Id, true));
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.RemoveSwipeAsync("guest", other.Games.First().Id));
        var gameId = night.Games.First().Id;
        await _voting.RecordSwipeAsync("guest", gameId, true);
        await _nights.CloseAsync(night.Id, _account);
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.RecordSwipeAsync("guest", gameId, false));
        Assert.ThrowsAsync<InvalidOperationException>(() => _voting.RemoveSwipeAsync("guest", gameId));
        Assert.That((await _voting.GetProgressAsync("guest"))!.Swipes[gameId], Is.True);
    }

    private Task<GameNight> NightAsync()
        => _nights.CreateAsync("Weekend vote", null, [new GameCandidate("Azul"), new GameCandidate("Dune")], _account);
}
