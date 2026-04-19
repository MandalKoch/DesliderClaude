using System.Security.Cryptography;

namespace DesliderClaude.Data.Services.Imps;

/// <summary>
/// Minimal PBKDF2-SHA256 hasher. Self-contained (no Identity framework needed).
/// Format: <c>v1.&lt;iterations&gt;.&lt;salt-b64&gt;.&lt;hash-b64&gt;</c> — versioned so we can
/// rotate parameters later without breaking existing hashes.
/// </summary>
internal static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    // OWASP 2023 recommendation for PBKDF2-SHA256.
    private const int Iterations = 600_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1") return false;
        if (!int.TryParse(parts[1], out var iterations)) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException) { return false; }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
