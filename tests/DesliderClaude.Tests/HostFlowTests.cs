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

    private static int _usernameCounter;
    private static string NewUsername() => $"host{Interlocked.Increment(ref _usernameCounter)}_{Guid.NewGuid():N}"[..24];

    private async Task RegisterAndSignInAsync(string username, string password = "hunter2-test")
    {
        await Page.GotoAsync("/register");
        await Page.GetByLabel("Username").FillAsync(username);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/");
    }

    [Test]
    public async Task Unauthenticated_home_shows_signin_cta_instead_of_host_button()
    {
        await Page.GotoAsync("/");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Sign in to host" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Host a new night" })).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Create_page_redirects_unauthenticated_to_signin()
    {
        await Page.GotoAsync("/create");
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/signin"));
    }

    [Test]
    public async Task Host_can_register_sign_in_create_a_night_and_close_voting()
    {
        await RegisterAndSignInAsync(NewUsername());

        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("E2E Test Night");
        await Page.GetByLabel("Target date").FillAsync("2026-05-01");
        await Page.GetByLabel("Extra games").FillAsync("Catan\nAzul\nWingspan");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Preview games" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/night/[a-z0-9\-]+/host$"));
        await Expect(Page.GetByText("You're hosting")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "E2E Test Night" })).ToBeVisibleAsync();

        var shareUrl = await Page.GetByLabel("Share link").InputValueAsync();
        Assert.That(shareUrl, Does.Match(@"^https?://[^/]+/night/[a-z0-9\-]+$"));

        await Expect(Page.GetByText("Catan")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Wingspan")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Close voting" }).ClickAsync();
        await Expect(Page.GetByText("Closed", new() { Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Host_dashboard_without_cookie_shows_not_your_night()
    {
        await RegisterAndSignInAsync(NewUsername());

        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("Stranger Danger");
        await Page.GetByLabel("Extra games").FillAsync("A\nB");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Preview games" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        var hostUrl = Page.Url;

        var freshContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var stranger = await freshContext.NewPageAsync();
        await stranger.GotoAsync(hostUrl);

        await Expect(stranger.GetByRole(AriaRole.Heading, new() { Name = "Not your night." })).ToBeVisibleAsync();
        await freshContext.CloseAsync();
    }

    [Test]
    public async Task Sign_out_from_account_returns_user_to_unauthenticated_state()
    {
        var username = NewUsername();
        await RegisterAndSignInAsync(username);

        await Page.GotoAsync("/account");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = username })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Sign in to host" })).ToBeVisibleAsync();
    }

    private async Task<string> CreateNightAsHostAsync(string nightName, string games = "A\nB\nC")
    {
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync(nightName);
        await Page.GetByLabel("Extra games").FillAsync(games);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Preview games" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        var url = new Uri(Page.Url);
        return url.AbsolutePath.Split('/')[2];
    }

    [Test]
    public async Task Anonymous_voter_is_prompted_for_a_callsign_on_each_new_night()
    {
        // Anonymous identity is per-night now — there's no cross-night cookie
        // that remembers the display name. Each new share code prompts again.
        await RegisterAndSignInAsync(NewUsername());
        var firstCode = await CreateNightAsHostAsync("Anon Test 1");
        var secondCode = await CreateNightAsHostAsync("Anon Test 2");

        var anonymous = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var anon = await anonymous.NewPageAsync();

        await anon.GotoAsync($"/night/{firstCode}");
        await anon.GetByLabel("Your callsign").FillAsync("Mystery Bob");
        await anon.GetByRole(AriaRole.Button, new() { Name = "Enter the deck" }).ClickAsync();
        await Expect(anon).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/night/{firstCode}/swipe$"));

        await anon.GotoAsync($"/night/{secondCode}");
        // Prompt shows up again — browser autocomplete may prefill the input,
        // but the server-side auto-join has no way to recognise the same visitor.
        await Expect(anon.GetByLabel("Your callsign")).ToBeVisibleAsync();

        await anonymous.CloseAsync();
    }

    [Test]
    public async Task Signed_in_voter_skips_the_join_prompt_and_goes_straight_to_swipe()
    {
        await RegisterAndSignInAsync(NewUsername());
        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("Auto-Join Night");
        await Page.GetByLabel("Extra games").FillAsync("A\nB\nC");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Preview games" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();
        var hostUrl = Page.Url;
        var shareCode = new Uri(hostUrl).AbsolutePath.Split('/')[2];

        // Sign out the host.
        await Page.GotoAsync("/account");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();

        // Sign in as a fresh voter account.
        await RegisterAndSignInAsync(NewUsername());

        // Land on the share URL — should skip /night/{code} join and drop us on /swipe.
        await Page.GotoAsync($"/night/{shareCode}");
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/night/{shareCode}/swipe$"));
    }

    [Test]
    public async Task Voter_can_change_and_remove_votes_from_management_page()
    {
        // Host signs in, creates a night, signs out.
        await RegisterAndSignInAsync(NewUsername());
        var shareCode = await CreateNightAsHostAsync("Votes Page Test", "Alpha\nBeta\nGamma");
        await Page.GotoAsync("/account");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();

        // Same browser, now anonymous: join and swipe every game.
        await Page.GotoAsync($"/night/{shareCode}");
        await Page.GetByLabel("Your callsign").FillAsync("Voter Vee");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Enter the deck" }).ClickAsync();

        for (int i = 0; i < 3; i++)
        {
            await Page.Locator("button[name='Form.Yes'][value='true']").First.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // After the 3rd swipe the server auto-bounces to the votes management page.
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/night/{shareCode}/votes$"));
        await Expect(Page.GetByText("3 / 3 voted")).ToBeVisibleAsync();
        await Expect(Page.Locator(".votes-badge.yes")).ToHaveCountAsync(3);

        // Flip the first row to No.
        await Page.Locator(".votes-row").First.GetByRole(AriaRole.Button, new() { Name = "No" }).ClickAsync();
        await Expect(Page.Locator(".votes-badge.no")).ToHaveCountAsync(1);

        // Remove the second row's vote.
        await Page.Locator(".votes-row").Nth(1).GetByRole(AriaRole.Button, new() { Name = "✕" }).ClickAsync();
        await Expect(Page.GetByText("2 / 3 voted")).ToBeVisibleAsync();
        await Expect(Page.Locator(".votes-badge.unvoted")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Created_night_appears_in_home_list_with_host_badge()
    {
        await RegisterAndSignInAsync(NewUsername());

        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("List-Check Night");
        await Page.GetByLabel("Extra games").FillAsync("A\nB\nC");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Preview games" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();

        await Page.GotoAsync("/");
        await Expect(Page.GetByText("List-Check Night")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Host", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Anonymous_voter_can_restore_session_from_link_in_a_fresh_browser()
    {
        // Host creates a night, signs out.
        await RegisterAndSignInAsync(NewUsername());
        var shareCode = await CreateNightAsHostAsync("Restore Test", "Alpha\nBeta\nGamma");
        await Page.GotoAsync("/account");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();

        // Anonymous join + one swipe.
        await Page.GotoAsync($"/night/{shareCode}");
        await Page.GetByLabel("Your callsign").FillAsync("Restorable Ray");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Enter the deck" }).ClickAsync();
        await Page.Locator("button[name='Form.Yes'][value='true']").First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Grab the restore link from the panel.
        await Page.GetByText("Bookmark this link to return later").ClickAsync();
        var restoreUrl = await Page.Locator(".restore-panel input[type='text']").InputValueAsync();
        Assert.That(restoreUrl, Does.Match($@"^https?://[^/]+/night/{shareCode}/restore/[0-9A-F]+$"));

        // Fresh browser context (= new cookie jar). Visiting the base night URL would
        // dump them at the callsign form; the restore URL must drop them on /swipe instead.
        var freshContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var fresh = await freshContext.NewPageAsync();
        await fresh.GotoAsync(restoreUrl);
        await Expect(fresh).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/night/{shareCode}/swipe$"));

        // The restored session retains the previous swipe (1/3 voted).
        await fresh.GotoAsync($"/night/{shareCode}/votes");
        await Expect(fresh.GetByText("1 / 3 voted")).ToBeVisibleAsync();
        await freshContext.CloseAsync();
    }

    [Test]
    public async Task Change_password_accepts_new_credentials_for_next_sign_in()
    {
        var username = NewUsername();
        await RegisterAndSignInAsync(username, "hunter2-test");

        await Page.GotoAsync("/account");
        await Page.GetByLabel("Current password").FillAsync("hunter2-test");
        await Page.GetByLabel("New password").FillAsync("swordfish-9000");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Update password" }).ClickAsync();
        await Expect(Page.GetByText("Password updated.")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();

        await Page.GotoAsync("/signin");
        await Page.GetByLabel("Username").FillAsync(username);
        await Page.GetByLabel("Password").FillAsync("swordfish-9000");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/");
    }
}
