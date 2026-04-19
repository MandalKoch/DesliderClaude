namespace DesliderClaude.Core.Options;

/// <summary>
/// Credentials for the site-wide admin area. Bind from the <c>Admin</c> config
/// section (or env vars <c>Admin__Username</c> / <c>Admin__Password</c>).
/// When either is unset, <c>/admin</c> returns 404 — not exposed at all.
/// </summary>
public sealed class AdminOptions
{
    public const string Section = "Admin";

    public string? Username { get; set; }
    public string? Password { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
