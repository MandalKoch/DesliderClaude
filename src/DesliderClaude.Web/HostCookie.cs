namespace DesliderClaude.Web;

internal static class HostCookie
{
    public static string Name(string shareCode) => $"deslider-host-{shareCode}";

    public static CookieOptions Options => new()
    {
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(365),
        SameSite = SameSiteMode.Lax,
        Path = "/",
    };
}
