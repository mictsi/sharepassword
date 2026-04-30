using System.Security.Cryptography;
using System.Text;

namespace SharePassword.Services;

public class AccessCodeService : IAccessCodeService
{
    public string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(AccessCodeFormat.Length);
        var chars = new char[AccessCodeFormat.Length];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = AccessCodeFormat.Alphabet[bytes[i] % AccessCodeFormat.Alphabet.Length];
        }

        return new string(chars);
    }

    public string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string code, string hash)
    {
        var candidate = HashCode(code);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(candidate),
            Encoding.UTF8.GetBytes(hash));
    }
}
