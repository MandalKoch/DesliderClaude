namespace DesliderClaude.Core.Models;

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Canonical login name. Lowercased at write time; unique.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// PBKDF2 hash from ASP.NET Core's <c>PasswordHasher&lt;User&gt;</c>. Null when
    /// the account was created via an external provider and has never had a
    /// local password.
    /// </summary>
    public string? PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();
}
