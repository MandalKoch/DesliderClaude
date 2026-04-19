using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DesliderClaude.Tests;

/// <summary>
/// Spins up the real Web app on a random loopback port so Playwright can drive it
/// through an actual browser. Uses an ephemeral SQLite file per fixture instance.
/// </summary>
public sealed class WebAppFixture : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"desliderclaude-test-{Guid.CreateVersion7()}.db");

    private IHost? _kestrelHost;

    public string ServerUrl { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:DesliderClaudeDb",
            $"Data Source={_dbPath};Cache=Shared");
    }

    // WebApplicationFactory requires its internal host to use TestServer, but Playwright
    // needs a real HTTP endpoint. The supported workaround is to build two hosts: a Kestrel
    // one that actually serves traffic, and a TestServer one we hand back so the factory
    // stays happy. See dotnet/aspnetcore#33846.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:0"));
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var server = _kestrelHost.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("No server addresses feature available.");
        ServerUrl = addresses.Addresses.First();

        testHost.Start();
        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kestrelHost?.Dispose();
        }
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best effort */ }
        }
    }
}
