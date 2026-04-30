using SharePassword.Services;

namespace SharePassword.Tests;

public class AccessCodeServiceTests
{
    private readonly AccessCodeService _service = new();

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
}
