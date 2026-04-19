using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class HostFlowTests : PageTest
{
    private WebAppFixture _app = null!;

    [OneTimeSetUp]
    public void StartApp()
    {
        _app = new WebAppFixture();
        _ = _app.Server; // forces lazy host creation
    }

    [OneTimeTearDown]
    public void StopApp() => _app.Dispose();

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true,
        BaseURL = _app.ServerUrl,
    };

    [Test]
    public async Task Host_can_create_a_night_and_close_voting()
    {
        // Landing page has the host-a-night entry point.
        await Page.GotoAsync("/");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Host a new night" })).ToBeVisibleAsync();

        // Fill the create form.
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("E2E Test Night");
        await Page.GetByLabel("Target date").FillAsync("2026-05-01");
        await Page.GetByLabel("Candidate games").FillAsync("Catan\nAzul\nWingspan");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();

        // Landed on the host dashboard.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        await Expect(Page.GetByText("You're hosting")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "E2E Test Night" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Open", new() { Exact = true })).ToBeVisibleAsync();

        // Share URL is populated with an absolute link.
        var shareUrl = await Page.GetByLabel("Share link").InputValueAsync();
        Assert.That(shareUrl, Does.Match(@"^https?://[^/]+/night/[a-z0-9\-]+$"));

        // All three candidates show up in the standings.
        await Expect(Page.GetByText("Catan")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Azul")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Wingspan")).ToBeVisibleAsync();

        // Close voting.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Close voting" }).ClickAsync();

        // Status flipped to closed; close button is gone.
        await Expect(Page.GetByText("Closed", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Close voting" })).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Host_dashboard_without_cookie_shows_not_your_night()
    {
        // Create a night via the authenticated fixture, then visit the host URL in a fresh
        // context that has no host cookie.
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("Stranger Danger");
        await Page.GetByLabel("Candidate games").FillAsync("A\nB");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        var hostUrl = Page.Url;

        var freshContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var stranger = await freshContext.NewPageAsync();
        await stranger.GotoAsync(hostUrl);

        await Expect(stranger.GetByRole(AriaRole.Heading, new() { Name = "Not your night." })).ToBeVisibleAsync();
        await freshContext.CloseAsync();
    }
}
