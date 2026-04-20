using System.Security.Claims;
using DesliderClaude.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DesliderClaude.Web;

internal static class AuthExtensions
{
    public static Task SignInUserAsync(this HttpContext http, User user)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("provider", "local"),
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    public static async Task SignOutUserAsync(this HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Sign-out wipes every deslider-* cookie, not just the auth one — the voter
        // tokens (deslider-voter-{shareCode}) are per-night login credentials too.
        var delete = new CookieOptions { Path = "/" };
        foreach (var name in http.Request.Cookies.Keys)
        {
            if (name.StartsWith("deslider-", StringComparison.Ordinal))
            {
                http.Response.Cookies.Delete(name, delete);
            }
        }
    }

    public static Guid? GetUserId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static string? GetUsername(this ClaimsPrincipal? principal)
        => principal?.FindFirst(ClaimTypes.Name)?.Value;
}
