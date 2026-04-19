namespace DesliderClaude.Core.Options;

/// <summary>
/// Config-driven admin allow-list. Bind from <c>Admin</c> section in appsettings /
/// env vars (<c>Admin__Usernames__0=andreas</c>). Usernames here get a <c>Role=Admin</c>
/// claim at sign-in and can reach <c>/admin</c>.
/// </summary>
public sealed class AdminOptions
{
    public const string Section = "Admin";

    public List<string> Usernames { get; set; } = new();
}
