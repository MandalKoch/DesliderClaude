using System.Security.Cryptography;

namespace DesliderClaude.Web;

internal static class VoterCookie
{
    public static string Name(string shareCode) => $"deslider-voter-{shareCode}";

    // Not HttpOnly: no sensitive privileges attached to this token, and leaving it readable
    // keeps the door open if we later want to read it client-side without another round trip.
    public static CookieOptions Options => new()
    {
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(60),
        SameSite = SameSiteMode.Lax,
        Path = "/",
    };

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
