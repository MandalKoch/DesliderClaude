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
        await Page.GetByLabel("Candidate games").FillAsync("Catan\nAzul\nWingspan");
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
        await Page.GetByLabel("Candidate games").FillAsync("A\nB");
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

    [Test]
    public async Task Created_night_appears_in_home_list_with_host_badge()
    {
        await RegisterAndSignInAsync(NewUsername());

        await Page.GotoAsync("/create");
        await Page.GetByLabel("Night name").FillAsync("List-Check Night");
        await Page.GetByLabel("Candidate games").FillAsync("A\nB\nC");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create night" }).ClickAsync();

        await Page.GotoAsync("/");
        await Expect(Page.GetByText("List-Check Night")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Host", new() { Exact = true })).ToBeVisibleAsync();
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
