namespace Sekura.Models;

public class InformationRequest
{
    public Guid Id { get; set; }
    public string PartnerEmail { get; set; } = string.Empty;
    public string RequestInstructions { get; set; } = string.Empty;
    public string EncryptedPartnerResponse { get; set; } = string.Empty;
    public string ResponseEncryptionMode { get; set; } = SecretEncryptionModes.ServerManaged;
    public string AccessCodeHash { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastSubmittedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool RequireOidcLogin { get; set; }
    public int FailedAccessAttempts { get; set; }
    public DateTime? AccessPausedUntilUtc { get; set; }
}
