using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class VoterManagementTests : PageTest
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
        IgnoreHTTPSErrors = true,
        BaseURL = _app.ServerUrl,
    };

    private static int _usernameCounter;
    private static string NewUsername() => $"vmgmt{Interlocked.Increment(ref _usernameCounter)}_{Guid.NewGuid():N}"[..24];

    private async Task RegisterAndSignInAsync(string username, string password = "hunter2-test")
    {
        await Page.GotoAsync("/register");
        await Page.GetByLabel("Username").FillAsync(username);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/");
    }

    private async Task<string> CreateNightAsync(string name, params string[] games)
    {
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync(name);
        foreach (var g in games)
        {
            await Page.GetByLabel("Add a single game").FillAsync(g);
            await Page.Locator("button[name='Form.Action'][value='add-text']").ClickAsync();
        }
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        return new Uri(Page.Url).AbsolutePath.Split('/')[2];
    }

    [Test]
    public async Task Host_invite_link_auto_joins_named_voter_on_open()
    {
        await RegisterAndSignInAsync(NewUsername());
        var shareCode = await CreateNightAsync("Invite Night", "Catan", "Azul", "Wingspan");

        // Pre-name an invite as host.
        await Page.Locator("input[name='Form.InviteName']").FillAsync("Alice Pre-named");
        await Page.Locator("button[name='Form.Action'][value='invite']").ClickAsync();

        // The success message renders the invite URL as a link — grab its href.
        var inviteLink = Page.Locator(".text-ok a").First;
        await Expect(inviteLink).ToBeVisibleAsync();
        var inviteUrl = await inviteLink.GetAttributeAsync("href");
        Assert.That(inviteUrl, Does.Match($@"/night/{shareCode}/invite/[0-9A-F]+$"));

        // Voter list now includes Alice with 0 swipes.
        await Expect(Page.Locator(".voter-row").Filter(new() { HasText = "Alice Pre-named" })).ToBeVisibleAsync();

        // Open the invite link in a fresh browser context — should land on /swipe
        // without seeing the callsign form.
        var freshContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var fresh = await freshContext.NewPageAsync();
        await fresh.GotoAsync(inviteUrl!);
        await Expect(fresh).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/night/{shareCode}/swipe$"));
        await freshContext.CloseAsync();
    }

    [Test]
    public async Task Host_can_remove_a_voter_and_their_swipes_disappear_from_ranking()
    {
        await RegisterAndSignInAsync(NewUsername());
        var shareCode = await CreateNightAsync("Remove Voter Night", "Alpha", "Beta", "Gamma");

        // Mint an invite for Bob.
        await Page.Locator("input[name='Form.InviteName']").FillAsync("Bob");
        await Page.Locator("button[name='Form.Action'][value='invite']").ClickAsync();
        var inviteUrl = await Page.Locator(".text-ok a").First.GetAttributeAsync("href");

        // Bob opens the invite in a fresh browser, casts one yes-swipe.
        var bobContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var bob = await bobContext.NewPageAsync();
        await bob.GotoAsync(inviteUrl!);
        await Expect(bob).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/night/{shareCode}/swipe$"));
        await bob.Locator("button[name='Form.Yes'][value='true']").First.ClickAsync();
        await bob.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await bobContext.CloseAsync();

        // Host refreshes the dashboard — Bob shows up with 1 swipe; total swipes = 1.
        await Page.GotoAsync($"/night/{shareCode}/host");
        await Expect(Page.Locator(".voter-row").Filter(new() { HasText = "Bob" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".host-stat").Filter(new() { HasText = "swipe" })).ToContainTextAsync("1");

        // Auto-accept the confirm() prompt, then click Bob's remove button.
        Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await Page.Locator(".voter-row")
            .Filter(new() { HasText = "Bob" })
            .Locator(".voter-remove-btn")
            .ClickAsync();

        // Bob's row is gone; swipe total is back to 0.
        await Expect(Page.Locator(".voter-row").Filter(new() { HasText = "Bob" })).ToHaveCountAsync(0);
        await Expect(Page.Locator(".host-stat").Filter(new() { HasText = "swipe" })).ToContainTextAsync("0");
    }

    [Test]
    public async Task Winner_page_shows_who_is_voting_to_anyone_with_the_link()
    {
        // Host creates night, signs out.
        await RegisterAndSignInAsync(NewUsername());
        var shareCode = await CreateNightAsync("Public Voters Night", "One", "Two", "Three");
        await Page.GotoAsync("/account");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();

        // Anonymous voter joins + casts one swipe.
        await Page.GotoAsync($"/night/{shareCode}");
        await Page.GetByLabel("Your callsign").FillAsync("Charlie Watcher");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Enter the deck" }).ClickAsync();
        await Page.Locator("button[name='Form.Yes'][value='true']").First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open winner page in a totally fresh browser (= no cookies for this night).
        var stranger = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var page = await stranger.NewPageAsync();
        await page.GotoAsync($"/night/{shareCode}/winner");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Who's voting" })).ToBeVisibleAsync();
        await Expect(page.Locator(".voter-row").Filter(new() { HasText = "Charlie Watcher" })).ToBeVisibleAsync();
        await Expect(page.Locator(".voter-row").Filter(new() { HasText = "Charlie Watcher" }))
            .ToContainTextAsync("1 / 3");
        await stranger.CloseAsync();
    }
}
