using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SharePassword.Options;

namespace SharePassword.Services;

public class AccessCodeService : IAccessCodeService
{
    private const string HmacHashPrefix = "HMACSHA256$";
    private const string HmacKeyDerivationLabel = "sharepassword.access-code.hmac.v1";
    private const int HmacKeyDerivationIterations = 100_000;
    private const int HmacKeySizeBytes = 32;

    private readonly byte[] _hmacKey;

    public AccessCodeService(IOptions<EncryptionOptions> encryptionOptions)
    {
        var passphrase = encryptionOptions.Value.Passphrase;

        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new InvalidOperationException("Encryption passphrase is not configured.");
        }

        _hmacKey = Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            Encoding.UTF8.GetBytes(HmacKeyDerivationLabel),
            HmacKeyDerivationIterations,
            HashAlgorithmName.SHA256,
            HmacKeySizeBytes);
    }

    public string GenerateCode()
    {
        return GenerateCode(AccessCodeFormat.Length);
    }

    public string GenerateCode(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Access code length must be greater than 0.");
        }

        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = AccessCodeFormat.Alphabet[bytes[i] % AccessCodeFormat.Alphabet.Length];
        }

        return new string(chars);
    }

    public string HashCode(string code)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return HmacHashPrefix + Convert.ToHexString(bytes);
    }

    public bool Verify(string code, string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var candidate = hash.StartsWith(HmacHashPrefix, StringComparison.Ordinal)
            ? HashCode(code)
            : ComputeLegacyHash(code);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(candidate),
            Encoding.UTF8.GetBytes(hash));
    }

    // Hash format used before HMAC keying was introduced; kept so shares created
    // prior to the upgrade stay accessible until they expire.
    private static string ComputeLegacyHash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}
