namespace DesliderClaude.Core.Models;

/// <summary>Per-voter snapshot for the host dashboard. <see cref="VoterToken"/>
/// is included so the host can build a per-voter invite link, e.g. for someone
/// pre-named with <c>CreateInviteAsync</c> who hasn't joined yet. The host page
/// is gated by ownership, so surfacing the token there is acceptable.</summary>
public sealed record VoterStatus(
    Guid VoterId,
    string DisplayName,
    string VoterToken,
    int SwipesCast,
    DateTimeOffset? LastSwipeAt);
