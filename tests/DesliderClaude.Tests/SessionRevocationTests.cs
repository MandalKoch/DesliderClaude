using System.Security.Claims;
using System.Text.RegularExpressions;
using DesliderClaude.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DesliderClaude.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public sealed class SessionRevocationTests : PageTest
{
    private const string Password = "session-test-password";
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
    public async Task Changing_password_keeps_current_session_and_rejects_a_copied_old_session()
    {
        var username = await RegisterAsync();
        var oldCookie = (await Context.CookiesAsync()).Single(c => c.Name == "deslider-auth");
        await using var oldSession = await Browser.NewContextAsync(new() { BaseURL = _app.ServerUrl });
        await oldSession.AddCookiesAsync([new Cookie { Name = oldCookie.Name, Value = oldCookie.Value, Url = _app.ServerUrl, HttpOnly = true }]);
        var oldPage = await oldSession.NewPageAsync();
        await oldPage.GotoAsync("/account");
        await Expect(oldPage.GetByRole(AriaRole.Heading, new() { Name = username, Exact = true })).ToBeVisibleAsync();
        await Page.GotoAsync("/account");
        await Page.GetByLabel("Current password", new() { Exact = true }).FillAsync(Password);
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync("updated-session-password");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Update password", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Password updated.", new() { Exact = true })).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = username, Exact = true })).ToBeVisibleAsync();
        await oldPage.ReloadAsync();
        await Expect(oldPage).ToHaveURLAsync(new Regex(@"/signin(?:\?|$)"));
        Assert.That((await oldSession.CookiesAsync()).Any(c => c.Name == "deslider-auth"), Is.False);
        Assert.That((await Context.CookiesAsync()).Single(c => c.Name == "deslider-auth").Value, Is.Not.EqualTo(oldCookie.Value));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Admin_password_reset_or_account_deletion_revokes_existing_session(bool deleteAccount)
    {
        var username = await RegisterAsync();
        await Page.GotoAsync("/account");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = username, Exact = true })).ToBeVisibleAsync();
        using (var scope = _app.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<IUserService>().SignInAsync(username, Password);
            var admin = scope.ServiceProvider.GetRequiredService<IAdminService>();
            if (deleteAccount) await admin.DeleteUserAsync(user!.Id);
            else await admin.SetUserPasswordAsync(user!.Id, "admin-reset-password");
        }
        await Page.ReloadAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/signin(?:\?|$)"));
        Assert.That((await Context.CookiesAsync()).Any(c => c.Name == "deslider-auth"), Is.False);
    }

    [TestCase(null)]
    [TestCase("malformed")]
    [TestCase("0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Missing_or_invalid_password_stamp_is_rejected(string? stamp)
    {
        using var scope = _app.Services.CreateScope();
        var username = $"stamp{Guid.CreateVersion7():N}"[..24];
        var user = await scope.ServiceProvider.GetRequiredService<IUserService>().RegisterAsync(username, Password);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };
        if (stamp is not null) claims.Add(new Claim("password-stamp", stamp));
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        using var client = _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", $"deslider-auth={options.TicketDataFormat.Protect(ticket)}");
        using var response = await client.GetAsync("/account");
        Assert.That((int)response.StatusCode, Is.EqualTo(302));
        Assert.That(response.Headers.Location!.AbsolutePath, Is.EqualTo("/signin"));
        Assert.That(response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("deslider-auth=;", StringComparison.Ordinal)), Is.True);
    }

    private async Task<string> RegisterAsync()
    {
        var username = $"session{Guid.CreateVersion7():N}"[..24];
        await Page.GotoAsync("/register");
        await Page.GetByLabel("Username", new() { Exact = true }).FillAsync(username);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create account", Exact = false }).ClickAsync();
        await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/");
        return username;
    }
}
