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
        _app.Bgg.Things[13] = new BggThingFetch(13, "boardgame", "Catan", "img/catan.jpg", "thumb/catan.jpg",
            MinPlayers: 3, MaxPlayers: 4, MinPlayTimeMinutes: 60, MaxPlayTimeMinutes: 120,
            RecommendedPlayerCounts: new[]
            {
                new BggPlayerCountVote(3, BggPlayerCountKind.Recommended),
                new BggPlayerCountVote(4, BggPlayerCountKind.Best),
            });
        _app.Bgg.Things[9209] = new BggThingFetch(9209, "boardgame", "Ticket to Ride", null, null,
            MinPlayers: 2, MaxPlayers: 5, MinPlayTimeMinutes: 30, MaxPlayTimeMinutes: 60,
            RecommendedPlayerCounts: Array.Empty<BggPlayerCountVote>());
        _app.Bgg.Things[266192] = new BggThingFetch(266192, "boardgame", "Wingspan", null, null,
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
    public async Task Create_night_from_imported_geeklist_with_player_filter()
    {
        await RegisterAndSignInAsync(NewUsername());

        await Page.GotoAsync("/account/libraries");
        await Page.GetByLabel("Geeklist ID / URL — or BGG username").FillAsync("4242");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();
        await Expect(Page.Locator(".libraries-row-name")).ToHaveTextAsync("My Test Geeklist");

        // /create is empty. Add the library → 3 rows. Add a manual game → 4 rows.
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("BGG Pick Night");

        await Page.GetByLabel("Add a BGG library").SelectOptionAsync(new SelectOptionValue { Label = "My Test Geeklist — 3 games" });
        await Page.Locator("button[name='Form.Action'][value='add-import']").ClickAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(3);

        await Page.GetByLabel("Add a single game").FillAsync("Sushi Go");
        await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(4);

        // Filter to min-players=5 — hides Catan (max=4). Manual Sushi Go stays visible.
        await Page.Locator("input[name='Form.MinPlayers']").FillAsync("5");
        await Page.Locator("button[name='Form.Action'][value='filter']").ClickAsync();

        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(3);
        var visible = (await Page.Locator(".candidate-row:not([hidden]) .candidate-name").AllInnerTextsAsync())
            .Select(n => n.ToLowerInvariant().Trim())
            .ToList();
        Assert.That(visible.Any(s => s.Contains("ticket to ride")), Is.True);
        Assert.That(visible.Any(s => s.Contains("wingspan")), Is.True);
        Assert.That(visible.Any(s => s.Contains("sushi go")), Is.True, "manual game must survive the filter");
        Assert.That(visible.Any(s => s.Contains("catan")), Is.False);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        await Expect(Page.GetByText("Ticket to Ride")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Sushi Go")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Swipe_card_renders_bgg_image_for_imported_game()
    {
        await RegisterAndSignInAsync(NewUsername());

        // Import geeklist, create night with all three games (no filter).
        await Page.GotoAsync("/account/libraries");
        await Page.GetByLabel("Geeklist ID / URL — or BGG username").FillAsync("4242");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();
        await Expect(Page.Locator(".libraries-row-name")).ToHaveTextAsync("My Test Geeklist");

        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("Image Test Night");
        await Page.GetByLabel("Add a BGG library").SelectOptionAsync(new SelectOptionValue { Label = "My Test Geeklist — 3 games" });
        await Page.Locator("button[name='Form.Action'][value='add-import']").ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        var shareCode = new Uri(Page.Url).AbsolutePath.Split('/')[2];

        // Sign out, anonymous join, land on swipe.
        await Page.GotoAsync("/account");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await Page.GotoAsync($"/night/{shareCode}");
        await Page.GetByLabel("Your callsign").FillAsync("Pixel Pat");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Enter the deck" }).ClickAsync();

        // Catan is the only one we seeded with an image. Cycle through cards until we land on it.
        // (PickNextGameAsync is deterministic unvoted-only, but order within that set isn't.)
        var catanArt = Page.Locator(".swipe-card-art[src='img/catan.jpg']");
        for (var i = 0; i < 3 && await catanArt.CountAsync() == 0; i++)
        {
            await Page.Locator("button[name='Form.Yes'][value='false']").First.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        await Expect(catanArt).ToBeVisibleAsync();
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
