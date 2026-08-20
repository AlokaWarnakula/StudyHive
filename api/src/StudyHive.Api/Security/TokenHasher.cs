using System.Security.Cryptography;
using System.Text;

namespace StudyHive.Api.Security;

/// <summary>
/// Refresh tokens are opaque random strings handed to the client once and never stored —
/// only their SHA-256 hash is persisted (refresh_tokens.token_hash). See
/// DOCS/StudyHive_Master_Project_Relay_Plan.html §10.
/// </summary>
public static class TokenHasher
{
    public static string GenerateOpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}
