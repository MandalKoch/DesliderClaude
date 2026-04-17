namespace DesliderClaude.Core.Models;

public sealed record VoterProgress(
    Guid VoterId,
    Guid GameNightId,
    string DisplayName,
    IReadOnlyDictionary<Guid, bool> Swipes);
