using DesliderClaude.Core.Models;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class GatheringFlowTests : PageTest
{
    private WebAppFixture _app = null!;

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
    public async Task Friends_plan_bring_list_vote_twice_and_record_actual_play()
    {
        await Page.GotoAsync("/register");
        await Page.GetByLabel("Username").FillAsync($"gather{Guid.CreateVersion7():N}"[..24]);
        await Page.GetByLabel("Password").FillAsync("gathering-test-password");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = "Plan a gathering" }).ClickAsync();
        await Page.Locator("#gathering-name").FillAsync("Cabin weekend");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create gathering" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Cabin weekend", Exact = true })).ToBeVisibleAsync();
        var gatheringUrl = Page.Url;

        foreach (var game in new[] { "Azul", "Cascadia" })
        {
            await Page.Locator("#game-name").FillAsync(game);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Add game", Exact = true }).ClickAsync();
            await Expect(Page.Locator(".game-card h3").Filter(new() { HasText = game })).ToBeVisibleAsync();
        }

        await using var guestContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl, ViewportSize = new() { Width = 390, Height = 844 } });
        var guest = await guestContext.NewPageAsync();
        await guest.GotoAsync(gatheringUrl);
        await guest.GetByLabel("Your name", new() { Exact = true }).FillAsync("Alex");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Join gathering" }).ClickAsync();
        await guest.Locator("#game-name").FillAsync("Dune");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Add game", Exact = true }).ClickAsync();
        await Expect(guest.GetByRole(AriaRole.Heading, new() { Name = "Dune", Exact = true })).ToBeVisibleAsync();
        await guest.Locator(".game-card").Filter(new() { Has = guest.GetByRole(AriaRole.Heading, new() { Name = "Azul", Exact = true }) })
            .GetByRole(AriaRole.Button, new() { Name = "I'd play this" }).ClickAsync();
        await Expect(guest.GetByRole(AriaRole.Button, new() { Name = "Add to bring list" })).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        foreach (var game in new[] { "Azul", "Cascadia", "Dune" })
        {
            var card = Page.Locator(".game-card").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = game, Exact = true }) });
            await card.GetByLabel("Who brings it?").SelectOptionAsync(new SelectOptionValue { Label = "Alex" });
            await card.GetByRole(AriaRole.Button, new() { Name = "Add to bring list" }).ClickAsync();
            await Expect(card.Locator(".packed-badge")).ToBeVisibleAsync();
        }
        await Expect(Page.Locator(".bring-list li")).ToHaveCountAsync(3);
        await Expect(Page.Locator(".bring-list li").First).ToContainTextAsync("Alex brings this");

        await Page.Locator("#vote-name").FillAsync("First game");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Start a game vote" }).ClickAsync();
        await Expect(Page.Locator(".round-list li")).ToHaveCountAsync(1);
        await guest.ReloadAsync();
        await guest.Locator(".round-list").GetByRole(AriaRole.Button, new() { Name = "Vote", Exact = true }).ClickAsync();
        await Expect(guest).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[^/]+/swipe$"));
        for (var i = 0; i < 3; i++)
            await guest.Locator("button[name='Form.Yes'][value='true']").First.ClickAsync();
        await Expect(guest).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/votes$"));
        await guest.GetByRole(AriaRole.Link, new() { Name = "Bring list & played games", Exact = false }).ClickAsync();
        await Expect(guest.GetByRole(AriaRole.Button, new() { Name = "Join gathering" })).ToHaveCountAsync(0);

        await Page.Locator("#played-game").SelectOptionAsync(new SelectOptionValue { Label = "Azul" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Mark as played" }).ClickAsync();
        await Expect(Page.Locator(".history-list li")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".history-list li").First).ToContainTextAsync("Azul");
        await Expect(Page.Locator("input[name='Form.GameIds']").First).Not.ToBeCheckedAsync();
        await Page.Locator("#vote-name").FillAsync("Next game");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Start a game vote" }).ClickAsync();
        await Expect(Page.Locator(".round-list li")).ToHaveCountAsync(2);
        await Expect(Page.Locator(".bring-list li")).ToHaveCountAsync(3);
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-gathering-mobile.png", FullPage = true });
        var overflow = await Page.EvaluateAsync<string>("JSON.stringify([...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > innerWidth + 1).map(e => ({tag:e.tagName,classes:e.className,width:e.getBoundingClientRect().width,right:e.getBoundingClientRect().right})))");
        Assert.That(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), Is.True, $"Gathering must fit a phone viewport: {overflow}");
        await Page.GotoAsync("/");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Cabin weekend", Exact = false })).ToBeVisibleAsync();
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-home-mobile.png", FullPage = true });
    }

    private async Task<string> CreateGatheringAsync(string name)
    {
        await Page.GotoAsync("/register");
        await Page.GetByLabel("Username").FillAsync($"picker{Guid.CreateVersion7():N}"[..24]);
        await Page.GetByLabel("Password").FillAsync("gathering-test-password");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/");
        await Page.GotoAsync("/gatherings/create");
        await Page.Locator("#gathering-name").FillAsync(name);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create gathering" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true })).ToBeVisibleAsync();
        return Page.Url;
    }

    private static BggThingFetch Thing(int id, string name, int maxPlayers = 6)
        => new(id, "boardgame", name, $"https://example.test/{id}.jpg", $"https://example.test/{id}-thumb.jpg",
            MinPlayers: 2, MaxPlayers: maxPlayers, MinPlayTimeMinutes: 30, MaxPlayTimeMinutes: 60,
            RecommendedPlayerCounts: Array.Empty<BggPlayerCountVote>());

    [Test]
    public async Task Gathering_library_is_an_explicit_filtered_add_remove_list_on_mobile()
    {
        const int count = 24;
        var ids = Enumerable.Range(200_000, count).ToArray();
        _app.Bgg.GeekLists[7000] = new BggGeekListFetch(7000, "Weekend library", ids);
        foreach (var id in ids) _app.Bgg.Things[id] = Thing(id, $"Game {id}", id is 200_000 or 200_016 ? 4 : 6);
        _app.Bgg.Searches["scroll games"] = ids;
        var gatheringUrl = await CreateGatheringAsync("Picker weekend");
        await Page.GotoAsync("/account/libraries");
        await Page.GetByLabel("Geeklist ID / URL — or BGG username").FillAsync("7000");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Import", Exact = false }).ClickAsync();
        await Expect(Page.Locator(".libraries-row-name")).ToHaveTextAsync("Weekend library");
        await Page.GotoAsync(gatheringUrl);
        await Page.GetByRole(AriaRole.Link, new() { Name = "Choose from BGG or your library", Exact = false }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{gatheringUrl}#choose-games");
        await Page.EvaluateAsync("window.gatheringPickerMarker = true");
        await Page.GetByLabel("Your library", new() { Exact = true }).SelectOptionAsync(new SelectOptionValue { Label = "Weekend library" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Browse library", Exact = true }).ScrollIntoViewIfNeededAsync();
        var browseScroll = await Page.EvaluateAsync<double>("scrollY");
        Assert.That(browseScroll, Is.GreaterThan(500));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Browse library", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(count);
        await AssertScrollStayedAsync(browseScroll, "Loading the library");
        await Expect(Page.Locator(".game-card")).ToHaveCountAsync(0);
        var libraryId = await Page.GetByLabel("Your library", new() { Exact = true }).InputValueAsync();
        var libraryUrl = $"{gatheringUrl}?source=library&library={libraryId}";

        var add = Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 200016 to gathering", Exact = true });
        await ScrollBeforeAsync(add);
        var nextGame = Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 200017 to gathering", Exact = true });
        var addedGamePosition = await add.EvaluateAsync<double>("element => element.getBoundingClientRect().top");
        await Page.EvaluateAsync("window.gatheringPickerMarker = true");
        await add.ClickAsync();
        await Expect(Page.Locator(".game-card")).ToHaveCountAsync(1);
        Assert.That(await Page.EvaluateAsync<bool>("window.gatheringPickerMarker === true"), Is.True, "Adding must update the existing document.");
        await Page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
        Assert.That(await nextGame.EvaluateAsync<double>("element => element.getBoundingClientRect().top"), Is.EqualTo(addedGamePosition).Within(8),
            "The next available game should replace the added game in the same place on screen.");
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(23);
        await Expect(Page.GetByLabel("Your library", new() { Exact = true })).ToHaveValueAsync(libraryId);

        await Page.Locator(".picker-filters summary").ClickAsync();
        await Page.GetByLabel("How many players?").FillAsync("5");
        var applyFilters = Page.GetByRole(AriaRole.Button, new() { Name = "Apply filters" });
        var filterScroll = await ScrollBeforeAsync(applyFilters);
        await applyFilters.ClickAsync();
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(22);
        await AssertScrollStayedAsync(filterScroll, "Applying filters", 160);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Game 200016", Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 200017 to gathering", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".game-card")).ToHaveCountAsync(2);
        await Expect(Page.GetByLabel("How many players?")).ToHaveValueAsync("5");
        var clearFilters = Page.GetByRole(AriaRole.Button, new() { Name = "Clear filters", Exact = true });
        var clearScroll = await ScrollBeforeAsync(clearFilters);
        await clearFilters.ClickAsync();
        await Expect(Page.GetByLabel("How many players?")).ToHaveValueAsync("");
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(22);
        await AssertScrollStayedAsync(clearScroll, "Clearing filters", 160);
        var remove = Page.GetByRole(AriaRole.Button, new() { Name = "Remove Game 200016 from suggestions", Exact = true });
        var removeScroll = await ScrollBeforeAsync(remove);
        await remove.ClickAsync();
        await Expect(Page.Locator(".game-card")).ToHaveCountAsync(1);
        await AssertScrollStayedAsync(removeScroll, "Removing a suggestion", 160);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 200016 to gathering", Exact = true })).ToBeVisibleAsync();

        var chosen = Page.Locator(".game-card").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Game 200017", Exact = true }) });
        await chosen.GetByRole(AriaRole.Button, new() { Name = "Add to bring list", Exact = true }).ClickAsync();
        await Expect(chosen.Locator(".packed-badge")).ToBeVisibleAsync();
        await Expect(chosen.GetByRole(AriaRole.Button, new() { Name = "Remove Game 200017 from suggestions", Exact = true })).ToBeDisabledAsync();
        await Page.GetByLabel("Search BoardGameGeek", new() { Exact = true }).FillAsync("scroll games");
        var search = Page.GetByRole(AriaRole.Button, new() { Name = "Search BGG", Exact = true });
        var searchScroll = await ScrollBeforeAsync(search);
        await search.ClickAsync();
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(19);
        await AssertScrollStayedAsync(searchScroll, "Searching BGG", 160);
        await Expect(Page.GetByLabel("Your library", new() { Exact = true })).ToHaveValueAsync(libraryId);

        await Page.GetByLabel("Suggest a game you can bring").FillAsync("Manual scroll game");
        var manualAdd = Page.GetByRole(AriaRole.Button, new() { Name = "Add game", Exact = true });
        var manualScroll = await ScrollBeforeAsync(manualAdd);
        await manualAdd.ClickAsync();
        await Expect(Page.Locator(".game-card")).ToHaveCountAsync(2);
        await AssertScrollStayedAsync(manualScroll, "Adding a game by name", 160);
        await Expect(Page.GetByLabel("Search BoardGameGeek", new() { Exact = true })).ToHaveValueAsync("scroll games");
        var interested = chosen.GetByRole(AriaRole.Button, new() { Name = "I'd play this", Exact = true });
        var interestScroll = await ScrollBeforeAsync(interested);
        await interested.ClickAsync();
        await Expect(interested).ToHaveAttributeAsync("aria-pressed", "true");
        await AssertScrollStayedAsync(interestScroll, "Saving interest", 160);
        Assert.That(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), Is.True, "Picker must fit the phone viewport.");
        await Page.Locator("#choose-games").EvaluateAsync("element => element.scrollIntoView({block: 'start'})");
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-gathering-picker-mobile.png" });
        foreach (var section in new[] { "suggestions", "bring-list", "play" })
        {
            var sectionLink = Page.Locator($".section-nav a[href='{gatheringUrl}#{section}']");
            await sectionLink.ClickAsync();
            await Expect(Page).ToHaveURLAsync($"{gatheringUrl}#{section}");
            await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(19);
            Assert.That(await Page.EvaluateAsync<bool>("window.gatheringPickerMarker === true"), Is.True, "Section links must stay in this gathering document.");
        }

        var skipLink = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content", Exact = true });
        await skipLink.FocusAsync();
        await skipLink.PressAsync("Enter");
        await Expect(Page).ToHaveURLAsync($"{gatheringUrl}#main-content");
        Assert.That(await Page.EvaluateAsync<bool>("window.gatheringPickerMarker === true"), Is.True, "Skipping to content must stay in the gathering.");

        await using var guestContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var guest = await guestContext.NewPageAsync();
        await guest.GotoAsync(gatheringUrl);
        await guest.GetByLabel("Your name", new() { Exact = true }).FillAsync("Library guest");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Join gathering" }).ClickAsync();
        await Expect(guest.Locator(".member-greeting")).ToContainTextAsync("Library guest");
        await guest.GotoAsync(libraryUrl);
        await Expect(guest.Locator("#library-source")).ToHaveCountAsync(0);
        await Expect(guest.Locator(".candidate-row")).ToHaveCountAsync(0);
        await Expect(guest.GetByRole(AriaRole.Button, new() { Name = "Remove Game 200017 from suggestions", Exact = true })).ToHaveCountAsync(0);

        async Task<double> ScrollBeforeAsync(ILocator control)
        {
            await control.ScrollIntoViewIfNeededAsync();
            var position = await Page.EvaluateAsync<double>("scrollY");
            Assert.That(position, Is.GreaterThan(500), "Exercise controls below the page heading.");
            return position;
        }

        async Task AssertScrollStayedAsync(double position, string action, double tolerance = 8)
        {
            await Page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
            Assert.That(await Page.EvaluateAsync<bool>("window.gatheringPickerMarker === true"), Is.True, $"{action} must keep the existing document.");
            Assert.That(await Page.EvaluateAsync<double>("scrollY"), Is.EqualTo(position).Within(tolerance), $"{action} must keep the current part of the page in view.");
        }
    }

    [Test]
    public async Task Guests_can_search_BGG_and_keep_manual_entry_when_lookup_fails()
    {
        _app.Bgg.Searches["guest game"] = new[] { 300_001, 300_002 };
        _app.Bgg.Things[300_001] = Thing(300_001, "Island Quest");
        _app.Bgg.Things[300_002] = Thing(300_002, "Island Friends");
        _app.Bgg.SearchFailures["lookup offline"] = new HttpRequestException("BGG unavailable");
        var gatheringUrl = await CreateGatheringAsync("Search weekend");
        await using var guestContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl, ViewportSize = new() { Width = 390, Height = 844 } });
        var guest = await guestContext.NewPageAsync();
        await guest.GotoAsync(gatheringUrl);
        await guest.GetByLabel("Your name", new() { Exact = true }).FillAsync("Search guest");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Join gathering" }).ClickAsync();
        await Expect(guest.Locator(".member-greeting")).ToContainTextAsync("Search guest");
        await guest.GetByLabel("Search BoardGameGeek", new() { Exact = true }).FillAsync("guest game");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Search BGG", Exact = true }).ClickAsync();
        await Expect(guest.Locator(".candidate-row")).ToHaveCountAsync(2);
        await Expect(guest.Locator(".candidate-row").First).ToContainTextAsync("2–6 players · 30–60 min");
        await Expect(guest.GetByRole(AriaRole.Link, new() { Name = "Open Island Quest on BoardGameGeek", Exact = true })).ToHaveAttributeAsync("href", "https://boardgamegeek.com/boardgame/300001");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Add Island Quest to gathering", Exact = true }).ClickAsync();
        await Expect(guest.GetByRole(AriaRole.Heading, new() { Name = "Island Quest", Exact = true })).ToBeVisibleAsync();
        await Expect(guest.Locator(".candidate-row")).ToHaveCountAsync(1);
        await guest.GetByRole(AriaRole.Button, new() { Name = "Remove Island Quest from suggestions", Exact = true }).ClickAsync();
        await Expect(guest.Locator(".game-card")).ToHaveCountAsync(0);
        await Expect(guest.Locator(".candidate-row")).ToHaveCountAsync(2);
        await guest.GetByLabel("Search BoardGameGeek", new() { Exact = true }).FillAsync("lookup offline");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Search BGG", Exact = true }).ClickAsync();
        await Expect(guest.GetByText("Couldn't load games from BoardGameGeek.", new() { Exact = false })).ToBeVisibleAsync();
        await guest.Locator("#game-name").FillAsync("Handwritten favourite");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Add game", Exact = true }).ClickAsync();
        await Expect(guest.GetByRole(AriaRole.Heading, new() { Name = "Handwritten favourite", Exact = true })).ToBeVisibleAsync();
        await Expect(guest.Locator(".game-card")).ToHaveCountAsync(1);
    }

}
