using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class GuestRejoinFlowTests : PageTest
{
    private WebAppFixture _app = null!;
    private const string Password = "guest-protection-test-password";

    [OneTimeSetUp]
    public void StartApp()
    {
        _app = new WebAppFixture();
        _ = _app.Server;
    }

    [OneTimeTearDown]
    public void StopApp() => _app.Dispose();

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = _app.ServerUrl,
        ViewportSize = new() { Width = 390, Height = 844 },
    };

    [Test]
    public async Task Night_guest_rejoins_and_protects_existing_votes_without_becoming_host()
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();
        var host = await users.RegisterAsync(NewUsername(), Password);
        var voting = scope.ServiceProvider.GetRequiredService<IVotingService>();
        var night = await scope.ServiceProvider.GetRequiredService<IGameNightService>()
            .CreateAsync("Guest continuity", null, [new GameCandidate("Azul"), new GameCandidate("Dune")], host.Id);
        var nightPath = $"/night/{night.ShareCode}";
        await Page.GotoAsync(nightPath);
        await Page.GetByLabel("Your name", new() { Exact = true }).FillAsync("Alex");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Join the vote", Exact = false }).ClickAsync();
        await Page.Locator("[data-swipe-yes]").ClickAsync();
        await Expect(Page.Locator(".swipe-progress-label")).ToContainTextAsync("1");
        var participant = (await voting.ListParticipantsAsync(night.Id)).Single();

        await Context.ClearCookiesAsync();
        await Page.GotoAsync(nightPath);
        await Page.GetByLabel("Continue as existing guest").SelectOptionAsync(participant.Id.ToString());
        await Page.GetByRole(AriaRole.Button, new() { Name = "Continue as this guest", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".swipe-progress-label")).ToContainTextAsync("1");
        Assert.That(await voting.GetVoterCountAsync(night.Id), Is.EqualTo(1));

        await Page.GetByRole(AriaRole.Link, new() { Name = "Protect this guest with an account", Exact = false }).ClickAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Create account to protect this guest", Exact = true }).ClickAsync();
        var ownerName = NewUsername();
        await RegisterAsync(Page, ownerName);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Protect Alex with my account", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Protect Alex with my account", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".swipe-progress-label")).ToContainTextAsync("1");

        await using var returningContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var returning = await returningContext.NewPageAsync();
        await SignInAsync(returning, ownerName);
        await returning.GotoAsync(nightPath);
        await Expect(returning.Locator(".swipe-progress-label")).ToContainTextAsync("1");
        Assert.That(await voting.GetVoterCountAsync(night.Id), Is.EqualTo(1));
        await returning.GotoAsync(nightPath + "/host");
        await Expect(returning.GetByRole(AriaRole.Heading, new() { Name = "Not your night.", Exact = true })).ToBeVisibleAsync();

        var credential = (await returningContext.CookiesAsync()).Single(cookie => cookie.Name == $"deslider-voter-{night.ShareCode}");
        await using var outsiderContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        await outsiderContext.AddCookiesAsync([new Cookie { Name = credential.Name, Value = credential.Value, Url = _app.ServerUrl }]);
        var outsider = await outsiderContext.NewPageAsync();
        await outsider.GotoAsync(nightPath + $"/restore/{credential.Value}");
        await Expect(outsider.GetByRole(AriaRole.Heading, new() { Name = "Can't restore this session.", Exact = true })).ToBeVisibleAsync();
        await outsider.GotoAsync(nightPath + "/votes");
        await Expect(outsider.GetByText("You need to join before you can see your votes.", new() { Exact = true })).ToBeVisibleAsync();
        await outsider.GotoAsync(nightPath);
        await Expect(outsider.Locator("#existing-guest option").Filter(new() { HasText = "Alex" })).ToHaveCountAsync(0);
        var otherName = NewUsername();
        await users.RegisterAsync(otherName, Password);
        await SignInAsync(outsider, otherName);
        await outsider.GotoAsync(nightPath + $"/restore/{credential.Value}");
        await Expect(outsider.GetByRole(AriaRole.Heading, new() { Name = "Can't restore this session.", Exact = true })).ToBeVisibleAsync();
        Assert.That((await voting.ListParticipantsAsync(night.Id)).Single().Id, Is.EqualTo(participant.Id));
    }

    [Test]
    public async Task Gathering_guest_rejoins_claims_votes_and_turnout_tracks_new_games()
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();
        var host = await users.RegisterAsync(NewUsername(), Password);
        var gatherings = scope.ServiceProvider.GetRequiredService<IGatheringService>();
        var gathering = await gatherings.CreateAsync("Shared weekend", host.Id);
        var path = $"/gathering/{gathering.ShareCode}";
        await Page.GotoAsync(path);
        await Page.GetByLabel("Your name", new() { Exact = true }).FillAsync("Bea");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Join gathering", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".member-greeting")).ToContainTextAsync("Bea");
        var member = (await gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Members.Single();
        var credential = (await Context.CookiesAsync()).Single(cookie => cookie.Name == $"deslider-gathering-{gathering.ShareCode}");
        await gatherings.AddGameAsync(gathering.Id, credential.Value, new GameCandidate("Azul"));
        await gatherings.AddGameAsync(gathering.Id, credential.Value, new GameCandidate("Dune"));
        await Page.GotoAsync(path + "/vote");
        for (var i = 0; i < 2; i++)
        {
            await Page.Locator("[data-swipe-yes]").ClickAsync();
            await Expect(Page.Locator(".swipe-progress-label")).ToContainTextAsync($"{i + 1}");
        }
        await Page.GotoAsync(path);
        await Page.Locator(".voting-progress summary").ClickAsync();
        await Expect(Page.Locator(".voting-progress li")).ToContainTextAsync("All games voted");

        await Context.ClearCookiesAsync();
        await Page.GotoAsync(path);
        await Page.GetByLabel("Continue as existing guest").ScrollIntoViewIfNeededAsync();
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-guest-join.png", FullPage = true });
        await Page.GetByLabel("Continue as existing guest").SelectOptionAsync(member.Id.ToString());
        await Page.GetByRole(AriaRole.Button, new() { Name = "Continue as this guest", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".member-greeting")).ToContainTextAsync("Bea");
        credential = (await Context.CookiesAsync()).Single(cookie => cookie.Name == $"deslider-gathering-{gathering.ShareCode}");
        await gatherings.AddGameAsync(gathering.Id, credential.Value, new GameCandidate("Everdell"));
        await Page.ReloadAsync();
        await Page.Locator(".voting-progress summary").ClickAsync();
        await Expect(Page.Locator(".voting-progress li")).ToContainTextAsync("2 of 3 voted");
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-gathering-progress.png", FullPage = true });
        Assert.That((await gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Members.Single().Id, Is.EqualTo(member.Id));

        await Page.GetByRole(AriaRole.Link, new() { Name = "Protect this guest with an account", Exact = false }).ClickAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Create account to protect this guest", Exact = true }).ClickAsync();
        var ownerName = NewUsername();
        await RegisterAsync(Page, ownerName);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Protect Bea with my account", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".member-greeting")).ToContainTextAsync("Bea");

        await using var returningContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var returning = await returningContext.NewPageAsync();
        await SignInAsync(returning, ownerName);
        await returning.GotoAsync(path);
        await Expect(returning.Locator(".member-greeting")).ToContainTextAsync("Bea");
        await Expect(returning.Locator(".host-options")).ToHaveCountAsync(0);
        await returning.GotoAsync(path + "/vote");
        await Expect(returning.Locator(".swipe-progress-label")).ToContainTextAsync("2");
        await Expect(returning.Locator("[data-swipe-card]")).ToContainTextAsync("Everdell");
        Assert.That((await gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Members.Single().Id, Is.EqualTo(member.Id));

        var protectedCookie = (await returningContext.CookiesAsync()).Single(cookie => cookie.Name == credential.Name);
        await using var outsiderContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        await outsiderContext.AddCookiesAsync([new Cookie { Name = protectedCookie.Name, Value = protectedCookie.Value, Url = _app.ServerUrl }]);
        var outsider = await outsiderContext.NewPageAsync();
        await outsider.GotoAsync(path + "/vote");
        await Expect(outsider.GetByRole(AriaRole.Heading, new() { Name = "Pull up a chair.", Exact = true })).ToBeVisibleAsync();
        await outsider.GotoAsync(path);
        await Expect(outsider.Locator("#existing-guest option").Filter(new() { HasText = "Bea" })).ToHaveCountAsync(0);
        var otherName = NewUsername();
        await users.RegisterAsync(otherName, Password);
        await SignInAsync(outsider, otherName);
        await outsider.GotoAsync(path + "/vote");
        await Expect(outsider.GetByRole(AriaRole.Heading, new() { Name = "Pull up a chair.", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Long_account_username_joins_both_flows_with_a_short_display_name()
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();
        var username = $"{Guid.CreateVersion7():N}{new string('a', 32)}";
        var account = await users.RegisterAsync(username, Password);
        var host = await users.RegisterAsync(NewUsername(), Password);
        var voting = scope.ServiceProvider.GetRequiredService<IVotingService>();
        var night = await scope.ServiceProvider.GetRequiredService<IGameNightService>()
            .CreateAsync("Long username night", null, [new GameCandidate("Azul")], host.Id);
        var gatherings = scope.ServiceProvider.GetRequiredService<IGatheringService>();
        var gathering = await gatherings.CreateAsync("Long username gathering", host.Id);
        await SignInAsync(Page, username);
        await Page.GotoAsync($"/night/{night.ShareCode}");
        await Expect(Page.Locator("[data-swipe-card]")).ToContainTextAsync("Azul");
        var voter = (await voting.ListParticipantsAsync(night.Id)).Single();
        Assert.That(voter.DisplayName, Is.EqualTo(username[..40]));
        Assert.That(voter.IsAccountProtected, Is.True);
        await Page.GotoAsync($"/night/{night.ShareCode}");
        await Expect(Page.Locator("[data-swipe-card]")).ToContainTextAsync("Azul");
        Assert.That((await voting.ListParticipantsAsync(night.Id)).Single().Id, Is.EqualTo(voter.Id));

        await Page.GotoAsync($"/gathering/{gathering.ShareCode}");
        await Expect(Page.Locator(".member-greeting")).ToContainTextAsync(username[..40]);
        var member = (await gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Members.Single();
        Assert.That(member.DisplayName, Is.EqualTo(username[..40]));
        Assert.That(member.UserId, Is.EqualTo(account.Id));
        await Page.ReloadAsync();
        await Expect(Page.Locator(".member-greeting")).ToContainTextAsync(username[..40]);
        Assert.That((await gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Members.Single().Id, Is.EqualTo(member.Id));
        Assert.That((await users.GetByIdAsync(account.Id))!.Username, Is.EqualTo(username));
    }

    private static string NewUsername() => $"rejoin{Guid.CreateVersion7():N}"[..24];

    private async Task RegisterAsync(IPage page, string username)
    {
        await page.GetByLabel("Username", new() { Exact = true }).FillAsync(username);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create account", Exact = false }).ClickAsync();
    }

    private async Task SignInAsync(IPage page, string username)
    {
        await page.GotoAsync("/signin");
        await page.GetByLabel("Username", new() { Exact = true }).FillAsync(username);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = false }).ClickAsync();
        await Expect(page).ToHaveURLAsync($"{_app.ServerUrl}/");
    }
}
