namespace DesliderClaude.Core.Models;

/// <summary>Join row between <see cref="BggImport"/> and <see cref="BggGame"/>.
/// Composite PK on (BggImportId, BggGameId).</summary>
public class BggImportItem
{
    public Guid BggImportId { get; set; }
    public BggImport Import { get; set; } = null!;

    public int BggGameId { get; set; }
    public BggGame Game { get; set; } = null!;
}
