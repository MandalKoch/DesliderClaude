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
public sealed class SecurityRegressionTests : PageTest
{
    private WebAppFixture _app = null!;
    private WebApplicationFactory<Program> _configuredApp = null!;
    private readonly string _adminPassword = $"security-{Guid.CreateVersion7():N}";

    [OneTimeSetUp]
    public void StartApp()
    {
        _app = new WebAppFixture();
        _configuredApp = _app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Username"] = "security-test-admin",
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
        HttpCredentials = new() { Username = "security-test-admin", Password = _adminPassword },
    };

    [TestCase("admin-user")]
    [TestCase("admin-voter")]
    [TestCase("host-voter")]
    [TestCase("admin-night-close")]
    [TestCase("admin-night-delete")]
    public async Task Confirmations_treat_user_supplied_names_as_text(string surface)
    {
        var payload = "'+(window.xssmarker=1)+'" + surface;
        using var scope = _configuredApp.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();
        var username = surface == "admin-user" ? payload : $"security{Guid.CreateVersion7():N}"[..24];
        const string password = "security-test-password";
        var host = await users.RegisterAsync(username, password);
        string path;
        ILocator remove;
        if (surface == "admin-user")
        {
            path = "/admin?tab=users";
            remove = Page.Locator("tr").Filter(new() { HasText = payload }).Locator("button[type=submit]").Last;
        }
        else
        {
            var night = await scope.ServiceProvider.GetRequiredService<IGameNightService>()
                .CreateAsync(surface.StartsWith("admin-night", StringComparison.Ordinal) ? payload : "Safe confirmation", null, [new GameCandidate("Azul")], host.Id);
            if (surface.EndsWith("voter", StringComparison.Ordinal))
                await scope.ServiceProvider.GetRequiredService<IVotingService>().CreateInviteAsync(night.Id, payload);
            path = surface switch
            {
                "admin-voter" => $"/admin/nights/{night.ShareCode}",
                "host-voter" => $"/night/{night.ShareCode}/host",
                _ => "/admin",
            };
            remove = surface switch
            {
                "admin-voter" => Page.Locator("tr").Filter(new() { HasText = payload }).Locator("button[type=submit]"),
                "admin-night-close" => Page.Locator("tr").Filter(new() { HasText = payload }).Locator("button[title='Close voting']"),
                "admin-night-delete" => Page.Locator("tr").Filter(new() { HasText = payload }).Locator("button[title='Delete night']"),
                _ => Page.Locator(".voter-remove-btn"),
            };
            if (surface == "host-voter")
            {
                await Page.GotoAsync("/signin");
                await Page.GetByLabel("Username", new() { Exact = true }).FillAsync(username);
                await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
                await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = false }).ClickAsync();
                await Expect(Page).ToHaveURLAsync($"{_app.ServerUrl}/");
            }
        }
        await Page.GotoAsync(path);
        string? confirmation = null;
        Page.Dialog += async (_, dialog) =>
        {
            confirmation = dialog.Message;
            await dialog.DismissAsync();
        };
        await remove.ClickAsync();
        Assert.That(confirmation, Does.Contain(payload), "The name must appear literally in the confirmation.");
        Assert.That(await Page.EvaluateAsync<bool>("window.xssmarker === undefined"), Is.True);
        await Expect(remove).ToBeVisibleAsync();
    }
}
