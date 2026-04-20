namespace DesliderClaude.Core.Models;

/// <summary>
/// Bound from the <c>Bgg</c> config section. <see cref="ApiToken"/> is the Bearer
/// token BGG hands out when you register an app at
/// <c>boardgamegeek.com/using_the_xml_api</c>. Since late Oct 2025 every XML
/// API endpoint is auth-only; without a token every request gets 401.
/// </summary>
public sealed class BggOptions
{
    public const string Section = "Bgg";

    public string? ApiToken { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiToken);
}
