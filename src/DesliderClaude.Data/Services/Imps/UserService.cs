using DesliderClaude.Core.Models;
using DesliderClaude.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DesliderClaude.Data.Services.Imps;

internal sealed partial class UserService : IUserService
{
    private readonly DesliderClaudeDbContext _db;
    private readonly ILogger<UserService> _logger;

    public UserService(DesliderClaudeDbContext db, ILogger<UserService> logger)
    {
        _db = db;
        _logger = logger;
    }

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
        LogUserRegistered(normalized, user.Id);
        return user;
    }

    public async Task<User?> SignInAsync(string username, string password, CancellationToken ct = default)
    {
        var normalized = Normalize(username);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == normalized, ct);
        if (user?.PasswordHash is null)
        {
            LogSignInFailed(normalized);
            return null;
        }
        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            LogSignInFailed(normalized);
            return null;
        }
        LogSignInSuccess(user.Id);
        return user;
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
        LogPasswordChanged(userId);
    }

    private static string Normalize(string username) => username.Trim().ToLowerInvariant();

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Registered user {Username} ({UserId})")]
    private partial void LogUserRegistered(string username, Guid userId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "User {UserId} signed in")]
    private partial void LogSignInSuccess(Guid userId);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning,
        Message = "Failed sign-in for username {Username}")]
    private partial void LogSignInFailed(string username);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information,
        Message = "User {UserId} changed password")]
    private partial void LogPasswordChanged(Guid userId);
}
