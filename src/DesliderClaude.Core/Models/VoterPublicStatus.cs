namespace DesliderClaude.Core.Models;

/// <summary>Public-facing voter snapshot, suitable for everyone-with-the-link
/// pages (swipe, winner). Deliberately omits the per-voter token — that one
/// only ever lives in <see cref="VoterStatus"/> for the host.</summary>
public sealed record VoterPublicStatus(
    string DisplayName,
    int SwipesCast,
    DateTimeOffset? LastSwipeAt);
