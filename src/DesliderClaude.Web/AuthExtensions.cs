using System.Security.Claims;
using DesliderClaude.Core.Models;
using DesliderClaude.Core.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace DesliderClaude.Web;

internal static class AuthExtensions
{
    public const string AdminRole = "Admin";

    public static Task SignInUserAsync(this HttpContext http, User user)
    {
        var admins = http.RequestServices.GetRequiredService<IOptions<AdminOptions>>().Value.Usernames;
        var isAdmin = admins.Contains(user.Username, StringComparer.OrdinalIgnoreCase);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("provider", "local"),
        };
        if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, AdminRole));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    public static Task SignOutUserAsync(this HttpContext http)
        => http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    public static Guid? GetUserId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static string? GetUsername(this ClaimsPrincipal? principal)
        => principal?.FindFirst(ClaimTypes.Name)?.Value;
}
