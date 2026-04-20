namespace DesliderClaude.Core.Models;

/// <summary>Flat projection used by the /account/libraries page.</summary>
public sealed record BggImportView(
    Guid Id,
    BggImportSource SourceType,
    string SourceRef,
    string Name,
    int GameCount,
    DateTimeOffset LastRefreshedAt);
