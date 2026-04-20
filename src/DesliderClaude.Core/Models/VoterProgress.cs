namespace DesliderClaude.Core.Models;

public sealed record VoterProgress(
    Guid VoterId,
    Guid GameNightId,
    string DisplayName,
    Guid? UserId,
    IReadOnlyDictionary<Guid, bool> Swipes);
