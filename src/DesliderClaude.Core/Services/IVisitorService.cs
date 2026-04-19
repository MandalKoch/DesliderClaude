using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

public interface IVisitorService
{
    Task<Visitor?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Create a new anonymous visitor and stamp the initial display name.</summary>
    Task<Visitor> CreateAsync(string token, string displayName, CancellationToken ct = default);

    /// <summary>Update display name (and bump <c>LastSeenAt</c>) for an existing visitor.</summary>
    Task<Visitor?> UpdateDisplayNameAsync(string token, string displayName, CancellationToken ct = default);

    /// <summary>Bump <c>LastSeenAt</c> for an existing visitor.</summary>
    Task TouchAsync(string token, CancellationToken ct = default);
}
