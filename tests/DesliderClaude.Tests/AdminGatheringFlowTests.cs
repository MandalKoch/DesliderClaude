using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class AdminGatheringFlowTests : PageTest
{
    private WebAppFixture _app = null!;
    private WebApplicationFactory<Program> _configuredApp = null!;
    private readonly string _adminPassword = $"admin-review-{Guid.CreateVersion7():N}";
    private const string GuestName = "O'Brien <b>Guest</b>";

    [OneTimeSetUp]
    public void StartApp()
    {
        _app = new WebAppFixture();
        _configuredApp = _app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Username"] = "gathering-test-admin",
                ["Admin:Password"] = _adminPassword,
            })));
        _ = _configuredApp.Server;
    }

    [OneTimeTearDown]
    public void StopApp()
    {
        _configuredApp.Dispose();
        _app.Dispose();
    }

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = _app.ServerUrl,
        ViewportSize = new() { Width = 390, Height = 844 },
        HttpCredentials = new() { Username = "gathering-test-admin", Password = _adminPassword },
    };

    [Test]
    public async Task Admin_tracks_gatherings_and_sees_each_members_game_choices()
    {
        using var scope = _configuredApp.Services.CreateScope();
        var host = await CreateAccountAsync(scope.ServiceProvider);
        var gatherings = scope.ServiceProvider.GetRequiredService<IGatheringService>();
        var gathering = await gatherings.CreateAsync("Admin cabin", host.Id);
        var accountToken = NewToken();
        var guestToken = NewToken();
        await gatherings.JoinAsync(gathering.Id, "Account Alice", accountToken, host.Id);
        await gatherings.JoinAsync(gathering.Id, GuestName, guestToken);
        foreach (var name in new[] { "Azul", "Cascadia", "Dune" })
            await gatherings.AddGameAsync(gathering.Id, accountToken, new GameCandidate(name), host.Id);
        var games = (await gatherings.GetByShareCodeAsync(gathering.ShareCode))!.Games;
        await gatherings.SetInterestAsync(gathering.Id, accountToken, games.Single(g => g.Name == "Azul").Id, true, host.Id);
        await gatherings.SetInterestAsync(gathering.Id, accountToken, games.Single(g => g.Name == "Cascadia").Id, false, host.Id);
        await gatherings.SetInterestAsync(gathering.Id, guestToken, games.Single(g => g.Name == "Azul").Id, false);
        await gatherings.SetInterestAsync(gathering.Id, guestToken, games.Single(g => g.Name == "Dune").Id, true);
        var other = await gatherings.CreateAsync("Other gathering", host.Id);
        var otherToken = NewToken();
        await gatherings.JoinAsync(other.Id, "Unrelated participant", otherToken);
        await gatherings.AddGameAsync(other.Id, otherToken, new GameCandidate("Unrelated game"));
        await gatherings.SetVotingClosedAsync(other.Id, host.Id, true);

        await Page.GotoAsync("/admin");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Gatherings", Exact = false }).ClickAsync();
        var row = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Admin cabin" });
        await Expect(row).ToContainTextAsync(host.Username);
        var values = await row.Locator("td").AllTextContentsAsync();
        Assert.That(values, Does.Contain("2"), "Two distinct members must be counted.");
        Assert.That(values, Does.Contain("3"), "Three gathering games must be counted.");
        Assert.That(values, Does.Contain("4"), "Play and Pass must both count as votes.");
        await row.GetByRole(AriaRole.Link, new() { Name = "Admin cabin", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/admin/gatherings/{gathering.ShareCode}");
        await AssertChoicesAsync(Page, "Account Alice", ("Azul", "Play"), ("Cascadia", "Pass"), ("Dune", "Not voted"));
        await AssertChoicesAsync(Page, GuestName, ("Azul", "Pass"), ("Cascadia", "Not voted"), ("Dune", "Play"));
        await Expect(Page.GetByText("Unrelated participant", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Unrelated game", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Page.GetByText(GuestName, new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator(".gathering-participants b")).ToHaveCountAsync(0);
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-admin-gathering-mobile.png", FullPage = true });
        await Page.SetViewportSizeAsync(1440, 1000);
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-admin-gathering-desktop.png", FullPage = true });

        await using var publicContext = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        var publicPage = await publicContext.NewPageAsync();
        await publicPage.GotoAsync($"/gathering/{gathering.ShareCode}");
        await Expect(publicPage.GetByRole(AriaRole.Heading, new() { Name = "Participants & votes", Exact = true })).ToHaveCountAsync(0);
        await Expect(publicPage.GetByRole(AriaRole.Table)).ToHaveCountAsync(0);
        await publicPage.Locator(".voting-progress summary").ClickAsync();
        await Expect(publicPage.Locator(".voting-progress")).ToContainTextAsync("2 of 3 voted");
    }

    [Test]
    public async Task Admin_night_detail_shows_individual_Play_Pass_and_unvoted_games()
    {
        using var scope = _configuredApp.Services.CreateScope();
        var host = await CreateAccountAsync(scope.ServiceProvider);
        var night = await scope.ServiceProvider.GetRequiredService<IGameNightService>()
            .CreateAsync("Admin vote review", null, [new GameCandidate("Azul"), new GameCandidate("Cascadia"), new GameCandidate("Dune")], host.Id);
        var voting = scope.ServiceProvider.GetRequiredService<IVotingService>();
        var accountToken = NewToken();
        var guestToken = NewToken();
        await voting.JoinAsync(night.Id, "Account Alice", accountToken, host.Id);
        await voting.JoinAsync(night.Id, GuestName, guestToken);
        await voting.RecordSwipeAsync(accountToken, night.Games.Single(g => g.Name == "Azul").Id, true, host.Id);
        await voting.RecordSwipeAsync(accountToken, night.Games.Single(g => g.Name == "Cascadia").Id, false, host.Id);
        await voting.RecordSwipeAsync(guestToken, night.Games.Single(g => g.Name == "Azul").Id, false);
        await voting.RecordSwipeAsync(guestToken, night.Games.Single(g => g.Name == "Dune").Id, true);
        await Page.GotoAsync($"/admin/nights/{night.ShareCode}");
        await AssertChoicesAsync(Page, "Account Alice", ("Azul", "Play"), ("Cascadia", "Pass"), ("Dune", "Not voted"));
        await AssertChoicesAsync(Page, GuestName, ("Azul", "Pass"), ("Cascadia", "Not voted"), ("Dune", "Play"));
    }

    [Test]
    public async Task Nested_admin_routes_require_valid_Basic_auth_and_disable_when_unconfigured()
    {
        var paths = new[] { "/admin?tab=gatherings", "/admin/gatherings/missing", "/admin/nights/missing" };
        using var client = _configuredApp.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var authorization in new AuthenticationHeaderValue?[]
        {
            null,
            new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("incorrect:incorrect"))),
        })
        {
            client.DefaultRequestHeaders.Authorization = authorization;
            foreach (var path in paths)
            {
                using var response = await client.GetAsync(path);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), path);
            }
        }
        using var disabledApp = new WebAppFixture();
        using var disabled = disabledApp.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Username"] = "", ["Admin:Password"] = "" })));
        using var disabledClient = disabled.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var path in paths)
        {
            using var response = await disabledClient.GetAsync(path);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), path);
        }
    }

    private async Task AssertChoicesAsync(IPage page, string participant, params (string Game, string Choice)[] expected)
    {
        var section = page.Locator(".admin-participant").Filter(new() { HasText = participant });
        await Expect(section).ToContainTextAsync(participant);
        await section.Locator("summary").ClickAsync();
        foreach (var (game, choice) in expected)
        {
            var row = section.Locator(".admin-choice-list li").Filter(new() { HasText = game });
            await Expect(row).ToContainTextAsync(choice);
        }
    }

    private static async Task<User> CreateAccountAsync(IServiceProvider services)
        => await services.GetRequiredService<IUserService>().RegisterAsync($"admintest{Guid.CreateVersion7():N}"[..24], "admin-flow-test-password");

    private static string NewToken() => Guid.CreateVersion7().ToString("N");
}
