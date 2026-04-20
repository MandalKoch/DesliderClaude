using DesliderClaude.Core.Models;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class BggImportTests : PageTest
{
    private WebAppFixture _app = null!;

    [OneTimeSetUp]
    public void StartApp()
    {
        _app = new WebAppFixture();
        _ = _app.Server;

        _app.Bgg.GeekLists[4242] = new BggGeekListFetch(4242, "My Test Geeklist", new[] { 13, 9209, 266192 });
        _app.Bgg.Things[13] = new BggThingFetch(13, "Catan", "img/catan.jpg", "thumb/catan.jpg",
            MinPlayers: 3, MaxPlayers: 4, MinPlayTimeMinutes: 60, MaxPlayTimeMinutes: 120,
            RecommendedPlayerCounts: new[]
            {
                new BggPlayerCountVote(3, BggPlayerCountKind.Recommended),
                new BggPlayerCountVote(4, BggPlayerCountKind.Best),
            });
        _app.Bgg.Things[9209] = new BggThingFetch(9209, "Ticket to Ride", null, null,
            MinPlayers: 2, MaxPlayers: 5, MinPlayTimeMinutes: 30, MaxPlayTimeMinutes: 60,
            RecommendedPlayerCounts: Array.Empty<BggPlayerCountVote>());
        _app.Bgg.Things[266192] = new BggThingFetch(266192, "Wingspan", null, null,
            MinPlayers: 1, MaxPlayers: 5, MinPlayTimeMinutes: 40, MaxPlayTimeMinutes: 70,
            RecommendedPlayerCounts: Array.Empty<BggPlayerCountVote>());
    }

    [OneTimeTearDown]
    public void StopApp() => _app.Dispose();

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true,
        BaseURL = _app.ServerUrl,
    };

    private static int _usernameCounter;
    private static string NewUsername() => $"bgg{Interlocked.Increment(ref _usernameCounter)}_{Guid.NewGuid():N}"[..24];

    private async Task RegisterAndSignInAsync(string username, string password = "hunter2-test")
    {
        await Page.GotoAsync("/register");
        await Page.GetByLabel("Username").FillAsync(username);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/");
    }

    [Test]
    public async Task Signed_in_user_can_import_a_geeklist_by_id()
    {
        await RegisterAndSignInAsync(NewUsername());

        await Page.GotoAsync("/account/libraries");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Your BGG libraries." })).ToBeVisibleAsync();

        await Page.GetByLabel("Geeklist ID / URL — or BGG username").FillAsync("4242");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();

        await Expect(Page.Locator(".libraries-row-name")).ToHaveTextAsync("My Test Geeklist");
        await Expect(Page.Locator(".libraries-row-meta")).ToContainTextAsync("3 games");
        await Expect(Page.GetByText("Geeklist #4242", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Importing_by_geeklist_url_extracts_the_id()
    {
        await RegisterAndSignInAsync(NewUsername());

        await Page.GotoAsync("/account/libraries");
        await Page.GetByLabel("Geeklist ID / URL — or BGG username")
            .FillAsync("https://boardgamegeek.com/geeklist/4242/some-slug");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();

        await Expect(Page.Locator(".libraries-row-name")).ToHaveTextAsync("My Test Geeklist");
    }

    [Test]
    public async Task Remove_button_deletes_the_import_from_the_list()
    {
        await RegisterAndSignInAsync(NewUsername());

        await Page.GotoAsync("/account/libraries");
        await Page.GetByLabel("Geeklist ID / URL — or BGG username").FillAsync("4242");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();
        await Expect(Page.Locator(".libraries-row-name")).ToHaveTextAsync("My Test Geeklist");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove" }).ClickAsync();
        await Expect(Page.Locator(".libraries-row-name")).ToHaveCountAsync(0);
        await Expect(Page.GetByText("No imports yet.", new() { Exact = false })).ToBeVisibleAsync();
    }
}
