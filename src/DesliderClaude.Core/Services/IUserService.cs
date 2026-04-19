using DesliderClaude.Core.Models;

namespace DesliderClaude.Core.Services;

public interface IUserService
{
    /// <summary>
    /// Create a new local user. Throws <see cref="InvalidOperationException"/>
    /// if the username is already taken.
    /// </summary>
    Task<User> RegisterAsync(string username, string password, CancellationToken ct = default);

    /// <summary>
    /// Verify username + password. Returns the user on success, <c>null</c>
    /// on any failure (wrong user, wrong password, no local password set).
    /// </summary>
    Task<User?> SignInAsync(string username, string password, CancellationToken ct = default);

    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Return <c>true</c> if a local user already owns this (normalized) username.</summary>
    Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Given a username the caller wanted but couldn't have, return one that's free.
    /// Tries <c>{desired}_1337</c> first, then random 4-digit suffixes.
    /// </summary>
    Task<string> SuggestAvailableUsernameAsync(string desired, CancellationToken ct = default);

    /// <summary>
    /// Change the local password. Throws on wrong current password or if the
    /// user has no local password (external-only account).
    /// </summary>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
}
