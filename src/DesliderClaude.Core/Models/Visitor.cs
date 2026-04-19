namespace DesliderClaude.Core.Models;

/// <summary>
/// A persistent anonymous identity — one row per browser that has used DesliderClaude
/// without logging in. Ties a chosen display name (and, through <see cref="Voter"/>,
/// every swipe they cast) to a long-lived cookie token so the same "Bob" is recognised
/// on every night they drop into.
/// </summary>
public class Visitor
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Random token; stored in the <c>deslider-visitor</c> cookie.</summary>
    public string Token { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
