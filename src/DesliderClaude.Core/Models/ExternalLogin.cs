namespace DesliderClaude.Core.Models;

/// <summary>
/// Links an external identity provider's stable user id to one of our Users.
/// Unused until Google/Apple land — the shape is here so those providers drop in
/// without a schema change.
/// </summary>
public class ExternalLogin
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    /// <summary>"Google", "Apple", "GitHub", ...</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Stable, opaque per-provider user id (the `sub` claim).</summary>
    public string ProviderUserId { get; set; } = string.Empty;

    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
