using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class CookiePrivacyTests : PageTest
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

    public override BrowserNewContextOptions ContextOptions() => new() { BaseURL = _app.ServerUrl };

    [Test]
    public async Task Joining_keeps_voting_credential_out_of_JavaScript_and_preserves_return_lifetime()
    {
        using var scope = _app.Services.CreateScope();
        var night = await scope.ServiceProvider.GetRequiredService<IGameNightService>()
            .CreateAsync("Cookie privacy", null, [new GameCandidate("Azul"), new GameCandidate("Dune")]);
        await Page.GotoAsync($"/night/{night.ShareCode}");
        await Page.GetByLabel("Your name", new() { Exact = true }).FillAsync("Cookie guest");
        var joinedAt = DateTimeOffset.UtcNow;
        await Page.GetByRole(AriaRole.Button, new() { Name = "Join the vote", Exact = false }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/night/{night.ShareCode}/swipe");

        var name = $"deslider-voter-{night.ShareCode}";
        var cookie = (await Context.CookiesAsync()).Single(c => c.Name == name);
        Assert.Multiple(() =>
        {
            Assert.That(cookie.HttpOnly, Is.True);
            Assert.That(cookie.Secure, Is.False, "HTTP local development must remain usable.");
            Assert.That(cookie.SameSite, Is.EqualTo(SameSiteAttribute.Lax));
            Assert.That(cookie.Path, Is.EqualTo("/"));
            Assert.That(cookie.Expires, Is.EqualTo(joinedAt.AddDays(60).ToUnixTimeSeconds()).Within(10));
        });
        Assert.That(await Page.EvaluateAsync<string>("document.cookie"), Does.Not.Contain(name));
    }

    [TestCase("invite")]
    [TestCase("restore")]
    public async Task Https_adoption_marks_voting_and_antiforgery_cookies_secure(string route)
    {
        using var scope = _app.Services.CreateScope();
        var night = await scope.ServiceProvider.GetRequiredService<IGameNightService>()
            .CreateAsync("Secure cookies", null, [new GameCandidate("Azul"), new GameCandidate("Dune")]);
        var voter = await scope.ServiceProvider.GetRequiredService<IVotingService>()
            .CreateInviteAsync(night.Id, "Invited guest");
        using var client = _app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        using var response = await client.GetAsync($"/night/{night.ShareCode}/{route}/{voter.VoterToken}");
        var voterCookie = response.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"deslider-voter-{night.ShareCode}=", StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(voterCookie, Does.Contain("; secure").IgnoreCase);
            Assert.That(voterCookie, Does.Contain("; httponly").IgnoreCase);
            Assert.That(voterCookie, Does.Contain("max-age=5184000").IgnoreCase);
        });
        using var signIn = await client.GetAsync("/signin");
        var antiforgeryCookie = signIn.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(antiforgeryCookie, Does.Contain("; secure").IgnoreCase);
            Assert.That(antiforgeryCookie, Does.Contain("; httponly").IgnoreCase);
            Assert.That(antiforgeryCookie, Does.Contain("samesite=strict").IgnoreCase);
        });
    }
}
