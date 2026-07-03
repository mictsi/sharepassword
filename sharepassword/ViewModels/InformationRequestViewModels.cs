using System.ComponentModel.DataAnnotations;
using SharePassword.Models;
using SharePassword.Services;

namespace SharePassword.ViewModels;

public class InformationRequestAccessViewModel
{
    [EmailAddress]
    [StringLength(256, ErrorMessage = "Email address cannot exceed 256 characters.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(InformationRequestAccessCodeFormat.Length, MinimumLength = InformationRequestAccessCodeFormat.Length, ErrorMessage = InformationRequestAccessCodeFormat.LengthErrorMessage)]
    [RegularExpression(InformationRequestAccessCodeFormat.ValidationPattern, ErrorMessage = InformationRequestAccessCodeFormat.InvalidFormatErrorMessage)]
    [Display(Name = "Access code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(32, MinimumLength = 32, ErrorMessage = "Invalid link token format.")]
    [RegularExpression("^[A-Fa-f0-9]{32}$", ErrorMessage = "Invalid link token format.")]
    public string Token { get; set; } = string.Empty;

    public bool RequireOidcLogin { get; set; }
    public bool HasSubmittedResponse { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastSubmittedAtUtc { get; set; }
}

public class InformationRequestResponseViewModel
{
    public Guid RequestId { get; set; }

    [Required]
    [StringLength(32, MinimumLength = 32, ErrorMessage = "Invalid link token format.")]
    [RegularExpression("^[A-Fa-f0-9]{32}$", ErrorMessage = "Invalid link token format.")]
    public string Token { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(256, ErrorMessage = "Email address cannot exceed 256 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(InformationRequestAccessCodeFormat.Length, MinimumLength = InformationRequestAccessCodeFormat.Length, ErrorMessage = InformationRequestAccessCodeFormat.LengthErrorMessage)]
    [RegularExpression(InformationRequestAccessCodeFormat.ValidationPattern, ErrorMessage = InformationRequestAccessCodeFormat.InvalidFormatErrorMessage)]
    public string Code { get; set; } = string.Empty;

    public string RequestInstructions { get; set; } = string.Empty;

    [StringLength(TextInputLimits.MaxPlaintextLength, ErrorMessage = "Information cannot exceed 10000 characters.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Information to share")]
    public string PartnerResponse { get; set; } = string.Empty;

    [Display(Name = "Protect with extra password")]
    public bool UseClientEncryption { get; set; }

    [StringLength(ClientEncryptedSecretPayload.MaxPayloadLength, ErrorMessage = "Encrypted response payload cannot exceed 60000 characters.")]
    public string ClientEncryptedPartnerResponsePayload { get; set; } = string.Empty;

    public string ExistingClientEncryptedPartnerResponsePayload { get; set; } = string.Empty;
    public string ResponseEncryptionMode { get; set; } = SecretEncryptionModes.ServerManaged;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastSubmittedAtUtc { get; set; }
    public string? SuccessMessage { get; set; }
    public bool RequireOidcLogin { get; set; }

    public bool IsClientEncrypted => SecretEncryptionModes.IsClientEncrypted(ResponseEncryptionMode);
    public bool HasResponse => LastSubmittedAtUtc.HasValue
        && (!string.IsNullOrWhiteSpace(PartnerResponse) || !string.IsNullOrWhiteSpace(ExistingClientEncryptedPartnerResponsePayload));
}

public class InformationRequestSubmittedViewModel
{
    public bool IsProtectedResponse { get; set; }
    public bool IsUpdate { get; set; }
}