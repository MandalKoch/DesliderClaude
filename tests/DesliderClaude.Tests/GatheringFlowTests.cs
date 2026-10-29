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
    public async Task Friends_add_games_vote_and_host_closes_the_gathering()
    {
        var gatheringUrl = await CreateGatheringAsync("Cabin weekend");
        await Page.GotoAsync(gatheringUrl + "/games");
        await AddManualAsync(Page, "Azul");
        await AddManualAsync(Page, "Cascadia");
        await Page.GotoAsync(gatheringUrl);
        await Expect(Page.Locator(".cover-card")).ToHaveCountAsync(2);
        await Expect(Page.Locator("#game-name")).ToHaveCountAsync(0);

        await using var guestContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl, ViewportSize = new() { Width = 390, Height = 844 }, IsMobile = true, HasTouch = true });
        var guest = await guestContext.NewPageAsync();
        await guest.GotoAsync(gatheringUrl);
        await guest.GetByLabel("Your name", new() { Exact = true }).FillAsync("Alex");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Join gathering" }).ClickAsync();
        await guest.GotoAsync(gatheringUrl + "/games");
        await AddManualAsync(guest, "Dune");
        await guest.GotoAsync(gatheringUrl);
        await Expect(guest.Locator(".cover-card")).ToHaveCountAsync(3);
        await Expect(guest.Locator(".cover-card").Filter(new() { Has = guest.GetByRole(AriaRole.Heading, new() { Name = "Dune", Exact = true }) })).ToContainTextAsync("Alex");
        await Expect(guest.GetByRole(AriaRole.Button, new() { Name = "Close voting", Exact = true })).ToHaveCountAsync(0);

        await guest.GotoAsync(gatheringUrl + "/vote");
        await guest.WaitForFunctionAsync("() => { const card = document.querySelector('[data-swipe-card]'); return card?._swipeHandlers && card.style.transition === '' && getComputedStyle(card).opacity === '1'; }");
        await guest.ScreenshotAsync(new() { Path = "/tmp/deslider-gathering-vote.png" });
        await guest.EvaluateAsync("window.gatheringVoteMarker = true");
        for (var i = 0; i < 3; i++)
        {
            await guest.EvaluateAsync("scrollTo(0, 100)");
            var voteScroll = await guest.EvaluateAsync<double>("scrollY");
            Assert.That(voteScroll, Is.GreaterThan(0));
            if (i == 0)
            {
                await guest.Locator("[data-swipe-card]").DispatchEventAsync("pointerdown", new { pointerId = 1, clientX = 260, clientY = 300, button = 0 });
                await guest.Locator("[data-swipe-card]").DispatchEventAsync("pointermove", new { pointerId = 1, clientX = 70, clientY = 300 });
                await guest.Locator("[data-swipe-card]").DispatchEventAsync("pointerup", new { pointerId = 1, clientX = 70, clientY = 300 });
            }
            else await guest.Locator("[data-swipe-yes]").TapAsync();
            await Expect(guest.Locator(".swipe-progress-label")).ToContainTextAsync($"{i + 1}");
            if (i < 2)
            {
                await guest.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
                Assert.That(await guest.EvaluateAsync<double>("scrollY"), Is.EqualTo(voteScroll).Within(16), "Repeated votes must keep the current part of the page in view.");
            }
            Assert.That(await guest.EvaluateAsync<bool>("window.gatheringVoteMarker === true"), Is.True, "Voting must update the existing document.");
        }
        await Expect(guest.Locator("[data-swipe-card]")).ToHaveCountAsync(0);
        await guest.GetByRole(AriaRole.Button, new() { Name = "Pass Azul", Exact = true }).TapAsync();
        await Expect(guest.GetByRole(AriaRole.Button, new() { Name = "Pass Azul", Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");

        await Page.GotoAsync(gatheringUrl + "/games");
        await AddManualAsync(Page, "Everdell");
        await guest.ReloadAsync();
        await Expect(guest.Locator("[data-swipe-card]")).ToContainTextAsync("Everdell");
        await guest.Locator("[data-swipe-no]").TapAsync();
        await Expect(guest.Locator("[data-swipe-card]")).ToHaveCountAsync(0);

        await Page.GotoAsync(gatheringUrl);
        await Page.Locator(".host-options summary").ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close voting", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".voting-status")).ToContainTextAsync("Voting closed");
        await Page.Locator(".host-options").EvaluateAsync("element => element.open = true");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Reopen voting", Exact = true })).ToBeVisibleAsync();
        await guest.GotoAsync(gatheringUrl + "/games");
        await Expect(guest.Locator("#game-name")).ToHaveCountAsync(0);
        await guest.GotoAsync(gatheringUrl + "/vote");
        await Expect(guest.Locator("[data-swipe-card]")).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reopen voting", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".open-status")).ToContainTextAsync("Voting open");
        await guest.GotoAsync(gatheringUrl + "/games");
        await AddManualAsync(guest, "Wingspan");
        await Page.GotoAsync(gatheringUrl);
        await Expect(Page.Locator(".cover-card")).ToHaveCountAsync(5);
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-gathering-landing.png", FullPage = true });
        await Page.SetViewportSizeAsync(1440, 1000);
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-gathering-desktop.png", FullPage = true });
        await Page.SetViewportSizeAsync(390, 844);
        Assert.That(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), Is.True);
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-gathering-mobile.png", FullPage = true });
    }

    private async Task AddManualAsync(IPage page, string name)
    {
        await page.Locator("#game-name").FillAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Add as text", Exact = true }).ClickAsync();
        await Expect(page.Locator("#game-name")).ToHaveValueAsync("");
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
    public async Task Gathering_library_picker_preserves_scroll_and_explicit_choices_on_mobile()
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
        await Page.GotoAsync(gatheringUrl + "/games");
        await Page.EvaluateAsync("window.gatheringPickerMarker = true");
        await Page.GetByLabel("Your library", new() { Exact = true }).SelectOptionAsync(new SelectOptionValue { Label = "Weekend library" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Browse library", Exact = true }).ScrollIntoViewIfNeededAsync();
        var browseScroll = await Page.EvaluateAsync<double>("scrollY");
        Assert.That(browseScroll, Is.GreaterThan(0));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Browse library", Exact = true }).ClickAsync();
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(count);
        await AssertScrollStayedAsync(browseScroll, "Loading the library");
        var libraryId = await Page.GetByLabel("Your library", new() { Exact = true }).InputValueAsync();
        var libraryUrl = $"{gatheringUrl}/games?source=library&library={libraryId}";

        var add = Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 200016 to gathering", Exact = true });
        Assert.That(await ScrollBeforeAsync(add), Is.GreaterThan(500), "Exercise additions well below the page heading.");
        var nextGame = Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 200017 to gathering", Exact = true });
        var addedGamePosition = await add.EvaluateAsync<double>("element => element.getBoundingClientRect().top");
        await Page.EvaluateAsync("window.gatheringPickerMarker = true");
        await add.ClickAsync();
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(23);
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
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Game 200017 to gathering", Exact = true }).ClickAsync();
        await Expect(Page.Locator("#game-name")).ToHaveValueAsync("");
        await Expect(Page.GetByLabel("How many players?")).ToHaveValueAsync("5");
        var clearFilters = Page.GetByRole(AriaRole.Button, new() { Name = "Clear filters", Exact = true });
        var clearScroll = await ScrollBeforeAsync(clearFilters);
        await clearFilters.ClickAsync();
        await Expect(Page.GetByLabel("How many players?")).ToHaveValueAsync("");
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(22);
        await AssertScrollStayedAsync(clearScroll, "Clearing filters", 160);
        await Page.Locator("#game-name").FillAsync("scroll games");
        var search = Page.GetByRole(AriaRole.Button, new() { Name = "Search on BGG", Exact = true });
        var searchScroll = await ScrollBeforeAsync(search);
        await search.ClickAsync();
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(18);
        await AssertScrollStayedAsync(searchScroll, "Searching BGG", 160);
        await Expect(Page.GetByLabel("Your library", new() { Exact = true })).ToHaveValueAsync(libraryId);

        await Page.Locator("#game-name").FillAsync("Manual scroll game");
        var manualAdd = Page.GetByRole(AriaRole.Button, new() { Name = "Add as text", Exact = true });
        var manualScroll = await ScrollBeforeAsync(manualAdd);
        await manualAdd.ClickAsync();
        await Expect(Page.Locator("#game-name")).ToHaveValueAsync("");
        await AssertScrollStayedAsync(manualScroll, "Adding a game by name", 160);
        await Expect(Page.Locator(".candidate-row")).ToHaveCountAsync(0);
        Assert.That(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), Is.True, "Picker must fit the phone viewport.");
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-gathering-picker-mobile.png" });

        await using var guestContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var guest = await guestContext.NewPageAsync();
        await guest.GotoAsync(gatheringUrl);
        await guest.GetByLabel("Your name", new() { Exact = true }).FillAsync("Library guest");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Join gathering" }).ClickAsync();
        await Expect(guest.Locator(".member-greeting")).ToContainTextAsync("Library guest");
        await guest.GotoAsync(libraryUrl);
        await Expect(guest.Locator("#library-source")).ToHaveCountAsync(0);
        await Expect(guest.Locator(".candidate-row")).ToHaveCountAsync(0);
        await Expect(guest.GetByRole(AriaRole.Button, new() { Name = "Remove Game 200017 from gathering", Exact = true })).ToHaveCountAsync(0);

        async Task<double> ScrollBeforeAsync(ILocator control)
        {
            await control.ScrollIntoViewIfNeededAsync();
            var position = await Page.EvaluateAsync<double>("scrollY");
            Assert.That(position, Is.GreaterThanOrEqualTo(0));
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
        await guest.GotoAsync(gatheringUrl + "/games");
        await guest.Locator("#game-name").FillAsync("guest game");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Search on BGG", Exact = true }).ClickAsync();
        await Expect(guest.Locator(".candidate-row")).ToHaveCountAsync(2);
        await Expect(guest.Locator(".candidate-row").First).ToContainTextAsync("2–6 players · 30–60 min");
        await Expect(guest.GetByRole(AriaRole.Link, new() { Name = "Open Island Quest on BoardGameGeek", Exact = true })).ToHaveAttributeAsync("href", "https://boardgamegeek.com/boardgame/300001");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Add Island Quest to gathering", Exact = true }).ClickAsync();
        await Expect(guest.Locator(".feedback[role=status]")).ToContainTextAsync("Game added");
        await Expect(guest.Locator(".candidate-row")).ToHaveCountAsync(1);
        await guest.Locator("#game-name").FillAsync("lookup offline");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Search on BGG", Exact = true }).ClickAsync();
        await Expect(guest.GetByText("Couldn't load games from BoardGameGeek.", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(guest.Locator("#game-name")).ToHaveValueAsync("lookup offline");
        await guest.GetByRole(AriaRole.Button, new() { Name = "Add as text", Exact = true }).ClickAsync();
        await Expect(guest.Locator("#game-name")).ToHaveValueAsync("");
        await guest.GotoAsync(gatheringUrl);
        await Expect(guest.Locator(".cover-card")).ToHaveCountAsync(2);
        await Expect(guest.GetByRole(AriaRole.Heading, new() { Name = "lookup offline", Exact = true })).ToBeVisibleAsync();
        await guest.Locator("summary[aria-label='Options for Island Quest']").ClickAsync();
        await guest.GetByRole(AriaRole.Button, new() { Name = "Remove Island Quest from gathering", Exact = true }).ClickAsync();
        await Expect(guest.Locator(".cover-card")).ToHaveCountAsync(1);
    }

}
