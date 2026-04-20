namespace DesliderClaude.Core.Models;

public enum BggImportSource
{
    GeekList = 0,
    Collection = 1,
}

/// <summary>A user's saved BGG import source — either a geeklist ID or a BGG
/// username whose owned collection we pull. Re-fetching is manual via refresh.</summary>
public class BggImport
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public BggImportSource SourceType { get; set; }

    /// <summary>Geeklist numeric ID (as string) or BGG username, depending on
    /// <see cref="SourceType"/>.</summary>
    public string SourceRef { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the UI. Filled from BGG on import
    /// (geeklist title or "@username"); user can't edit today.</summary>
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastRefreshedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<BggImportItem> Items { get; set; } = new List<BggImportItem>();
}
