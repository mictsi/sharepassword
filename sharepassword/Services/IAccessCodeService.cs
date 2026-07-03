namespace SharePassword.Services;

public interface IAccessCodeService
{
    string GenerateCode();
    string GenerateCode(int length);
    string HashCode(string code);
    bool Verify(string code, string hash);
}
