namespace DesliderClaude.Core.Models;

/// <summary>Shape accepted by <see cref="Services.IGameNightService.CreateAsync"/>.
/// BggGameId is set when the candidate came from an imported library; free-text
/// games leave it null and also skip ImageUrl / ThumbnailUrl.</summary>
public sealed record GameCandidate(
    string Name,
    int? BggGameId = null,
    string? ImageUrl = null,
    string? ThumbnailUrl = null);
