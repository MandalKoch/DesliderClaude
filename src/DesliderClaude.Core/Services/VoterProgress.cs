namespace DesliderClaude.Core.Services;

public sealed record VoterProgress(
    Guid VoterId,
    Guid GameNightId,
    string DisplayName,
    IReadOnlyDictionary<Guid, bool> Swipes);
