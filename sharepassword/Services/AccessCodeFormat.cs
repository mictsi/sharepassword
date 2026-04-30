namespace SharePassword.Services;

public static class AccessCodeFormat
{
    public const int Length = 10;
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789#-";
    public const string ValidationPattern = "^[A-Za-z0-9#-]{10}$";
    public const string LengthErrorMessage = "Access code must be exactly 10 characters.";
    public const string InvalidFormatErrorMessage = "Access code may contain uppercase letters, lowercase letters, numbers, #, and - only.";

    public static bool IsValid(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != Length)
        {
            return false;
        }

        return code.All(ch => char.IsLetterOrDigit(ch) || ch is '#' or '-');
    }
}