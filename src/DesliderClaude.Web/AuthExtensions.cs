using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
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
            new Claim("password-stamp", PasswordStamp(user)),
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal.GetUserId();
        var user = userId is Guid id
            ? await context.HttpContext.RequestServices.GetRequiredService<IUserService>().GetByIdAsync(id, context.HttpContext.RequestAborted)
            : null;
        var stamp = context.Principal?.FindFirst("password-stamp")?.Value;
        if (user is not null && stamp?.Length == 64 && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(PasswordStamp(user)), Encoding.UTF8.GetBytes(stamp))) return;

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static string PasswordStamp(User user)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash ?? string.Empty)));

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

    public static string SafeReturnUrl(string? candidate)
        => candidate?.StartsWith('/') == true && Microsoft.AspNetCore.Http.HttpResults.RedirectHttpResult.IsLocalUrl(candidate)
            ? candidate
            : "/";

    public static Guid? GetUserId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static string? GetUsername(this ClaimsPrincipal? principal)
        => principal?.FindFirst(ClaimTypes.Name)?.Value;
}
