namespace Sekura.Services;

public static class InformationRequestAccessCodeFormat
{
    public const int Length = 15;
    public const string Alphabet = AccessCodeFormat.Alphabet;
    public const string ValidationPattern = "^[A-Za-z0-9#-]{15}$";
    public const string LengthErrorMessage = "Access code must be exactly 15 characters.";
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
