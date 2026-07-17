namespace Sekura.Models;

public static class TextInputLimits
{
    public const int MaxPlaintextLength = 10_000;
    public const int MaxClientEncryptedPayloadLength = 60_000;
    public const int MaxClientEncryptedCiphertextBytes = 40_016;
}