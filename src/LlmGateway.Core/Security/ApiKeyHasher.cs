using System.Security.Cryptography;
using System.Text;

namespace LlmGateway.Core.Security;

public sealed class ApiKeyHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;

    public (string Hash, string Salt) Hash(string apiKey)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(apiKey, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string apiKey, string expectedHash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(apiKey, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSize);
        var expected = Convert.FromBase64String(expectedHash);
        return CryptographicOperations.FixedTimeEquals(hash, expected);
    }

    public static string ComputeLookup(string apiKey)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(digest);
    }
}
