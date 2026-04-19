using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data.Services.Imps;

internal sealed class UserService : IUserService
{
    private readonly DesliderClaudeDbContext _db;

    public UserService(DesliderClaudeDbContext db) => _db = db;

    public async Task<User> RegisterAsync(string username, string password, CancellationToken ct = default)
    {
        var normalized = Normalize(username);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Username cannot be empty.");

        if (await _db.Users.AnyAsync(u => u.Username == normalized, ct))
            throw new InvalidOperationException("Username already taken.");

        var user = new User
        {
            Username = normalized,
            PasswordHash = PasswordHasher.Hash(password),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User?> SignInAsync(string username, string password, CancellationToken ct = default)
    {
        var normalized = Normalize(username);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == normalized, ct);
        if (user?.PasswordHash is null) return null;
        return PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("User not found.");
        if (user.PasswordHash is null)
            throw new InvalidOperationException("This account has no local password — sign in via the external provider instead.");
        if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect.");

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await _db.SaveChangesAsync(ct);
    }

    private static string Normalize(string username) => username.Trim().ToLowerInvariant();
}
