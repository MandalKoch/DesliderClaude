using System.Security.Cryptography;

namespace DesliderClaude.Web;

/// <summary>
/// Long-lived cross-night cookie that identifies an anonymous Visitor. Separate
/// from <see cref="VoterCookie"/>, which is per-share-code and expires sooner.
/// </summary>
internal static class VisitorCookie
{
    public const string CookieName = "deslider-visitor";

    public static CookieOptions Options => new()
    {
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(365),
        SameSite = SameSiteMode.Lax,
        Path = "/",
    };

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
