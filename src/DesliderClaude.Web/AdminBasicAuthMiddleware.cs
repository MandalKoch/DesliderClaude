using System.Security.Cryptography;
using System.Text;
using DesliderClaude.Core.Options;
using Microsoft.Extensions.Options;

namespace DesliderClaude.Web;

/// <summary>
/// HTTP Basic Auth gate for <c>/admin</c>. Credentials come from <see cref="AdminOptions"/>
/// (env: <c>Admin__Username</c> / <c>Admin__Password</c>). When unconfigured, the
/// endpoint 404s so its existence isn't advertised.
/// </summary>
internal static class AdminBasicAuth
{
    public static IApplicationBuilder UseAdminBasicAuth(this IApplicationBuilder app) =>
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/admin"),
            branch => branch.Use(Challenge));

    private static async Task Challenge(HttpContext ctx, RequestDelegate next)
    {
        var opts = ctx.RequestServices.GetRequiredService<IOptions<AdminOptions>>().Value;
        if (!opts.IsConfigured)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!TryParseBasicHeader(ctx.Request.Headers.Authorization.ToString(), out var user, out var pass)
            || !FixedTimeEquals(user, opts.Username!)
            || !FixedTimeEquals(pass, opts.Password!))
        {
            ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"admin\", charset=\"UTF-8\"";
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(ctx);
    }

    private static bool TryParseBasicHeader(string header, out string user, out string pass)
    {
        user = pass = string.Empty;
        const string prefix = "Basic ";
        if (string.IsNullOrEmpty(header) || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[prefix.Length..].Trim()));
            var sep = decoded.IndexOf(':');
            if (sep < 0) return false;
            user = decoded[..sep];
            pass = decoded[(sep + 1)..];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        if (aBytes.Length != bBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
