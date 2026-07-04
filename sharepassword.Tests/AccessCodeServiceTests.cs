using SharePassword.Options;
using SharePassword.Services;
using System.Security.Cryptography;
using System.Text;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SharePassword.Tests;

public class AccessCodeServiceTests
{
    private readonly AccessCodeService _service = new(MsOptions.Create(new EncryptionOptions
    {
        Passphrase = "unit-test-passphrase-1234567890-abcdef"
    }));

    [Fact]
    public void GenerateCode_Returns10Chars_UsingExpectedAlphabet()
    {
        var code = _service.GenerateCode();

        Assert.Equal(AccessCodeFormat.Length, code.Length);
        Assert.Matches(AccessCodeFormat.ValidationPattern, code);
    }

    [Fact]
    public void Verify_ReturnsTrueForCorrectCode_FalseForIncorrectCode()
    {
        const string code = "Ab3#dE7-f9";
        var hash = _service.HashCode(code);

        Assert.True(_service.Verify(code, hash));
        Assert.False(_service.Verify("zz3#dE7-f8", hash));
    }

    [Fact]
    public void HashCode_ProducesKeyedHmacFormat()
    {
        var hash = _service.HashCode("Ab3#dE7-f9");

        Assert.StartsWith("HMACSHA256$", hash);
        Assert.Equal("HMACSHA256$".Length + 64, hash.Length);
    }

    [Fact]
    public void HashCode_DependsOnPassphrase()
    {
        var otherService = new AccessCodeService(MsOptions.Create(new EncryptionOptions
        {
            Passphrase = "different-passphrase-1234567890-abcdef"
        }));

        Assert.NotEqual(_service.HashCode("Ab3#dE7-f9"), otherService.HashCode("Ab3#dE7-f9"));
    }

    [Fact]
    public void Verify_AcceptsLegacyUnkeyedSha256Hash()
    {
        const string code = "Ab3#dE7-f9";
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

        Assert.True(_service.Verify(code, legacyHash));
        Assert.False(_service.Verify("zz3#dE7-f8", legacyHash));
    }
}
