namespace Sekura.Options;

public class EncryptionOptions
{
    public const string SectionName = "Encryption";
    public const int MinimumPassphraseLength = 15;

    public string Passphrase { get; set; } = string.Empty;

    public static bool IsValidPassphrase(string? passphrase)
    {
        return !string.IsNullOrWhiteSpace(passphrase)
            && passphrase.Trim().Length >= MinimumPassphraseLength;
    }
}
