namespace SharePassword.Models;

public class LocalUserPasskey
{
    public Guid Id { get; set; }
    public Guid LocalUserId { get; set; }

    /// <summary>WebAuthn credential ID, base64url encoded.</summary>
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>COSE public key, base64 encoded.</summary>
    public string PublicKey { get; set; } = string.Empty;

    public long SignatureCounter { get; set; }
    public string Transports { get; set; } = string.Empty;
    public Guid AaGuid { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
}
