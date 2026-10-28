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

    private async Task BrowseLibraryAsync()
    {
        await RegisterAndSignInAsync(NewUsername());
        await Page.GotoAsync("/account/libraries");
        await Page.GetByLabel("Geeklist ID / URL — or BGG username").FillAsync("4242");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();
        await Expect(Page.Locator(".libraries-row-name")).ToHaveTextAsync("My Test Geeklist");
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Add a BGG library").SelectOptionAsync(new SelectOptionValue { Label = "My Test Geeklist — 3 games" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Browse library" }).ClickAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(3);
    }

    private async Task AddLibraryGameAsync(string name)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = $"Add {name} to this night", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".shortlist-row").Filter(new() { HasText = name })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Night_contains_only_games_explicitly_added_from_filtered_library()
    {
        await BrowseLibraryAsync();
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create night" })).ToBeDisabledAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(3);
        await Page.GetByLabel("Add a single game").FillAsync("Sushi Go");
        await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(1);

        await Page.Locator(".game-filters summary").ClickAsync();
        await Page.GetByLabel("How many players?").FillAsync("5");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply filters" }).ClickAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(2);
        await AddLibraryGameAsync("Ticket to Ride");
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(2);
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(1);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        await Expect(Page.GetByText("Ticket to Ride", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Sushi Go", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Catan", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Wingspan", new() { Exact = true })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Filters_keep_shortlisted_games_visible_and_remove_returns_them_to_library()
    {
        await BrowseLibraryAsync();
        await AddLibraryGameAsync("Catan");
        await Page.Locator(".game-filters summary").ClickAsync();
        await Page.GetByLabel("How many players?").FillAsync("5");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply filters" }).ClickAsync();
        await Expect(Page.Locator(".shortlist-row").Filter(new() { HasText = "Catan" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(2);
        await AddLibraryGameAsync("Ticket to Ride");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear filters" }).ClickAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(1);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove Catan from this night" }).ClickAsync();
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(2);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add Catan to this night" })).ToBeVisibleAsync();
        await AddLibraryGameAsync("Wingspan");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        await Expect(Page.GetByText("Ticket to Ride", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Wingspan", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Catan", new() { Exact = true })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Library_and_manual_entries_share_one_shortlist_game()
    {
        await BrowseLibraryAsync();
        await Page.GetByLabel("Add a single game").FillAsync("catan");
        await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".shortlist-row .candidate-name")).ToHaveTextAsync("Catan");
        await Expect(Page.Locator(".shortlist-row img")).ToHaveAttributeAsync("src", "thumb/catan.jpg");
        await Page.GetByLabel("Add a single game").FillAsync("Catan");
        await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(1);
        await Expect(Page.GetByText("Already on the list.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create night" })).ToBeDisabledAsync();

        await Page.GotoAsync("/create");
        await Page.GetByLabel("Add a single game").FillAsync("Catan");
        await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(1);
        await Page.GetByLabel("Add a BGG library").SelectOptionAsync(new SelectOptionValue { Label = "My Test Geeklist — 3 games" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Browse library" }).ClickAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(2);
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create night" })).ToBeDisabledAsync();
    }

    [Test]
    public async Task Adding_and_removing_games_preserves_position_on_a_phone()
    {
        const int gameCount = 24;
        var ids = Enumerable.Range(100_000, gameCount).ToArray();
        _app.Bgg.GeekLists[4243] = new BggGeekListFetch(4243, "Long game library", ids);
        foreach (var id in ids)
            _app.Bgg.Things[id] = new BggThingFetch(id, "boardgame", $"Game {id}", null, null,
                MinPlayers: 2, MaxPlayers: 4, MinPlayTimeMinutes: 30, MaxPlayTimeMinutes: 60,
                RecommendedPlayerCounts: Array.Empty<BggPlayerCountVote>());

        await Page.SetViewportSizeAsync(390, 844);
        await RegisterAndSignInAsync(NewUsername());
        await Page.GotoAsync("/account/libraries");
        await Page.GetByLabel("Geeklist ID / URL — or BGG username").FillAsync("4243");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();
        await Expect(Page.Locator(".libraries-row-name")).ToHaveTextAsync("Long game library");
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Add a BGG library").SelectOptionAsync(new SelectOptionValue { Label = "Long game library — 24 games" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Browse library" }).ClickAsync();
        await Expect(Page.Locator(".candidate-row:not([hidden])")).ToHaveCountAsync(gameCount);

        var addGame = Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 100016 to this night", Exact = true });
        await addGame.ScrollIntoViewIfNeededAsync();
        var originalScroll = await Page.EvaluateAsync<double>("window.scrollY");
        Assert.That(originalScroll, Is.GreaterThan(1000), "Exercise a game well below the page heading.");
        await Page.EvaluateAsync("window.selectionDocumentMarker = true");
        await addGame.ClickAsync();
        await Expect(Page.Locator(".candidate-count")).ToHaveTextAsync("1 game in this night");
        Assert.That(await Page.EvaluateAsync<bool>("window.selectionDocumentMarker === true"), Is.True);
        Assert.That(await Page.EvaluateAsync<double>("window.scrollY"), Is.GreaterThan(originalScroll - 250),
            "Adding a game must not jump to the page heading.");
        await AddLibraryGameAsync("Game 100000");
        await AddLibraryGameAsync("Game 100001");
        var removeGame = Page.GetByRole(AriaRole.Button, new() { Name = "Remove Game 100016 from this night" });
        await removeGame.ScrollIntoViewIfNeededAsync();
        originalScroll = await Page.EvaluateAsync<double>("window.scrollY");
        await removeGame.ClickAsync();
        await Expect(Page.Locator(".candidate-count")).ToHaveTextAsync("2 games in this night");
        Assert.That(await Page.EvaluateAsync<bool>("window.selectionDocumentMarker === true"), Is.True);
        Assert.That(await Page.EvaluateAsync<double>("window.scrollY"), Is.GreaterThan(originalScroll - 250),
            "Removing a game must not jump to the page heading.");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 100016 to this night", Exact = true })).ToBeVisibleAsync();
        await Page.Locator(".night-shortlist").ScrollIntoViewIfNeededAsync();
        Assert.That(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), Is.True,
            "The source controls and game lists must fit a phone viewport.");
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-night-shortlist-mobile.png", FullPage = true });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        await Expect(Page.GetByText("Game 100000", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Game 100016", new() { Exact = true })).ToHaveCountAsync(0);
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
        await Page.GetByRole(AriaRole.Button, new() { Name = "Browse library" }).ClickAsync();
        await AddLibraryGameAsync("Catan");
        await AddLibraryGameAsync("Ticket to Ride");
        await AddLibraryGameAsync("Wingspan");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        var shareCode = new Uri(Page.Url).AbsolutePath.Split('/')[2];

        // Sign out, anonymous join, land on swipe.
        await Page.GotoAsync("/account");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await Page.GotoAsync($"/night/{shareCode}");
        await Page.GetByLabel("Your name").FillAsync("Pixel Pat");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Join the vote" }).ClickAsync();

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
    public async Task Search_BGG_adds_only_the_chosen_game_with_authoritative_details()
    {
        _app.Bgg.Searches["catan"] = new[] { 13, 266192 };
        await Page.SetViewportSizeAsync(390, 844);
        await RegisterAndSignInAsync(NewUsername());
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Game name on BGG").FillAsync("catan");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search BGG", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".bgg-search-row")).ToHaveCountAsync(2);
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Open Catan on BGG" })).ToHaveAttributeAsync("href", "https://boardgamegeek.com/boardgame/13");
        var callsBeforeAdd = _app.Bgg.SearchCalls.Count;
        await Page.EvaluateAsync("window.bggSearchDocumentMarker = true");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Catan from BGG", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".shortlist-row .candidate-name")).ToHaveTextAsync("Catan");
        await Expect(Page.Locator(".shortlist-row img")).ToHaveAttributeAsync("src", "thumb/catan.jpg");
        await Expect(Page.Locator(".bgg-search-row")).ToHaveCountAsync(2);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add Catan from BGG", Exact = true })).ToBeDisabledAsync();
        await Expect(Page.GetByLabel("Game name on BGG")).ToHaveValueAsync("catan");
        Assert.That(_app.Bgg.SearchCalls, Has.Count.EqualTo(callsBeforeAdd), "Adding a result must reuse cached details, not repeat the search.");
        Assert.That(await Page.EvaluateAsync<bool>("window.bggSearchDocumentMarker === true"), Is.True);
        Assert.That(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), Is.True);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear search", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".bgg-search-row")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(1);
        await Page.GetByLabel("Add a single game").FillAsync("Azul");
        await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(2);

        await Page.Locator("input[name='Form.CandNames']").First.EvaluateAsync("element => element.value = 'Forged name'");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        await Expect(Page.GetByText("Catan", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Azul", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Wingspan", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Forged name", new() { Exact = true })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task BGG_search_validation_and_outage_leave_manual_entry_available()
    {
        _app.Bgg.SearchFailures["unavailable"] = new HttpRequestException("BGG unavailable");
        await RegisterAndSignInAsync(NewUsername());
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Game name on BGG").FillAsync("a");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search BGG", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("between 2 and 100 characters");
        await Page.GetByLabel("Game name on BGG").FillAsync("unavailable");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Search BGG", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("BGG search is unavailable");
        await Page.GetByLabel("Add a single game").FillAsync("Dune");
        await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        await Expect(Page.Locator(".shortlist-row .candidate-name")).ToHaveTextAsync("Dune");
        await Page.GetByLabel("Add a single game").FillAsync("Cascadia");
        await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        await Expect(Page.Locator(".shortlist-row")).ToHaveCountAsync(2);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
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
