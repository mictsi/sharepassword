using System.ComponentModel.DataAnnotations;
using SharePassword.Services;

namespace SharePassword.ViewModels;

public class ShareAccessViewModel
{
    [EmailAddress]
    [StringLength(256, ErrorMessage = "Email address cannot exceed 256 characters.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(AccessCodeFormat.Length, MinimumLength = AccessCodeFormat.Length, ErrorMessage = AccessCodeFormat.LengthErrorMessage)]
    [RegularExpression(AccessCodeFormat.ValidationPattern, ErrorMessage = AccessCodeFormat.InvalidFormatErrorMessage)]
    [Display(Name = "Access code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(32, MinimumLength = 32, ErrorMessage = "Invalid link token format.")]
    [RegularExpression("^[A-Fa-f0-9]{32}$", ErrorMessage = "Invalid link token format.")]
    public string Token { get; set; } = string.Empty;
    public bool RequireOidcLogin { get; set; }
}
