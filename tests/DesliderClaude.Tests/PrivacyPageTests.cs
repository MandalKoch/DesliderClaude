using System.Text.RegularExpressions;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class PrivacyPageTests : PageTest
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
    public async Task Privacy_draft_is_reachable_before_account_or_guest_data_entry()
    {
        await Page.GotoAsync("/");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Privacy", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/privacy");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Draft — details still to be completed", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.Locator(".privacy-page")).ToContainTextAsync("Operator/contact details still to be completed.");
        Assert.That(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), Is.True);

        using var scope = _app.Services.CreateScope();
        var host = await scope.ServiceProvider.GetRequiredService<IUserService>()
            .RegisterAsync($"privacy{Guid.CreateVersion7():N}"[..24], "privacy-test-password");
        var night = await scope.ServiceProvider.GetRequiredService<IGameNightService>()
            .CreateAsync("Privacy night", null, [new GameCandidate("Azul")], host.Id);
        var gathering = await scope.ServiceProvider.GetRequiredService<IGatheringService>()
            .CreateAsync("Privacy gathering", host.Id);
        foreach (var path in new[] { "/register", $"/night/{night.ShareCode}", $"/gathering/{gathering.ShareCode}" })
        {
            await Page.GotoAsync(path);
            var notice = Page.GetByRole(AriaRole.Link, new() { Name = "privacy notice", Exact = true });
            await Expect(notice).ToHaveAttributeAsync("href", "/privacy");
            await notice.ClickAsync();
            await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/privacy");
        }
        await Page.ScreenshotAsync(new() { Path = "/tmp/deslider-privacy-mobile.png", FullPage = true });
    }

    [Test]
    public async Task Fonts_load_from_the_app_without_Google_Fonts_requests()
    {
        var fontRequests = new List<string>();
        Page.Request += (_, request) =>
        {
            var host = new Uri(request.Url).Host;
            if (host is "fonts.googleapis.com" or "fonts.gstatic.com") fontRequests.Add(request.Url);
        };
        await Page.GotoAsync("/privacy");
        await Page.EvaluateAsync("() => document.fonts.ready.then(() => true)");
        var cssUrl = await Page.Locator("link[rel=stylesheet][href*='fonts/fonts']").GetAttributeAsync("href");
        var cssUri = new Uri(new Uri(_app.ServerUrl), cssUrl!);
        using var client = new HttpClient();
        var css = await client.GetStringAsync(cssUri);
        var fontUrls = Regex.Matches(css, @"url\(([^)]+)\)")
            .Select(match => match.Groups[1].Value.Trim('\'', '"'))
            .Distinct().ToArray();
        Assert.That(fontUrls, Is.Not.Empty, "The font stylesheet must reference bundled fonts.");
        foreach (var path in fontUrls)
        {
            var fontUri = new Uri(cssUri, path);
            Assert.That(fontUri.Host, Is.EqualTo(new Uri(_app.ServerUrl).Host));
            using var response = await client.GetAsync(fontUri);
            Assert.That(response.IsSuccessStatusCode, Is.True, $"Bundled font must be served: {path}");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.That(bytes.Take(4), Is.EqualTo("wOF2"u8.ToArray()), $"Expected a WOFF2 font: {path}");
        }
        Assert.That(fontRequests, Is.Empty, "Rendering the app must not contact Google Fonts.");
    }
}
