using System.Security.Cryptography;

namespace DesliderClaude.Web;

internal static class VoterCookie
{
    public static string Name(string shareCode) => $"deslider-voter-{shareCode}";

    public static CookieOptions Options(bool isHttps) => new()
    {
        IsEssential = true,
        HttpOnly = true,
        Secure = isHttps,
        MaxAge = TimeSpan.FromDays(60),
        SameSite = SameSiteMode.Lax,
        Path = "/",
    };

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
