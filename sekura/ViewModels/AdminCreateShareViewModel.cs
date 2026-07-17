using System.ComponentModel.DataAnnotations;
using Sekura.Models;

namespace Sekura.ViewModels;

public class AdminCreateShareViewModel
{
    [Required]
    [EmailAddress]
    [StringLength(256, ErrorMessage = "Recipient email cannot exceed 256 characters.")]
    [Display(Name = "Recipient email")]
    public string RecipientEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(256, ErrorMessage = "Username cannot exceed 256 characters.")]
    [Display(Name = "Username")]
    public string SharedUsername { get; set; } = string.Empty;

    [StringLength(TextInputLimits.MaxPlaintextLength, ErrorMessage = "Password or secret cannot exceed 10000 characters.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Password or secret")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Protect with extra password")]
    public bool UseClientEncryption { get; set; }

    [StringLength(ClientEncryptedSecretPayload.MaxPayloadLength, ErrorMessage = "Encrypted secret payload cannot exceed 60000 characters.")]
    public string ClientEncryptedPasswordPayload { get; set; } = string.Empty;

    [StringLength(TextInputLimits.MaxPlaintextLength, ErrorMessage = "Instructions cannot exceed 10000 characters.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Instructions")]
    public string Instructions { get; set; } = string.Empty;

    [Range(1, 168)]
    [Display(Name = "Expiration")]
    public int ExpiryHours { get; set; } = 4;

    [Display(Name = "Require Microsoft Entra ID sign-in")]
    public bool RequireOidcLogin { get; set; }

    public bool IsOidcLoginRequirementAvailable { get; set; }
}
