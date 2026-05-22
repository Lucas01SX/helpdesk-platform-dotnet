using System.Security.Cryptography;
using System.Text;
using Helpdesk.Modules.Identity.Application.Interfaces;
using Konscious.Security.Cryptography;

namespace Helpdesk.Modules.Identity.Infrastructure.Security;

internal sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 4;
    private const int MemorySize = 65536; // 64 MB
    private const int DegreeOfParallelism = 2;

    // Computed once at startup in the same format as Hash() output.
    // Guarantees GetDummyHash() → Verify() always runs the full KDF even when
    // the hasher format changes, preventing timing-based email enumeration gaps.
    private static readonly string CachedDummyHash = ComputeDummyHash();

    private static string ComputeDummyHash()
    {
        var salt = new byte[SaltSize]; // all-zero salt is valid for Argon2
        var hash = ComputeHash("timing-dummy", salt);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public string GetDummyHash() => CachedDummyHash;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2) return false;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expectedHash = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = ComputeHash(password, salt);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            Iterations = Iterations,
            MemorySize = MemorySize,
            DegreeOfParallelism = DegreeOfParallelism
        };
        return argon2.GetBytes(HashSize);
    }
}
