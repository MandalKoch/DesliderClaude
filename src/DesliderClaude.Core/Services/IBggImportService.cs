using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

public interface IBggImportService
{
    Task<BggImport> ImportGeekListAsync(Guid userId, int geekListId, CancellationToken ct = default);
    Task<BggImport> ImportCollectionAsync(Guid userId, string username, CancellationToken ct = default);

    /// <summary>Re-fetches the source and replaces items + the BggGame cache
    /// entries it points at. User must own the import.</summary>
    Task RefreshAsync(Guid importId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<BggImportView>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid importId, Guid userId, CancellationToken ct = default);

    /// <summary>Union of games from the user's selected imports, deduplicated by
    /// BggGameId. Used by the /create flow to present a filterable candidate set.</summary>
    Task<IReadOnlyList<BggCandidateGame>> ListCandidatesAsync(
        Guid userId,
        IReadOnlyList<Guid> importIds,
        CancellationToken ct = default);
}
