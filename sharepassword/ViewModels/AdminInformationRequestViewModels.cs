using System.ComponentModel.DataAnnotations;
using SharePassword.Models;

namespace SharePassword.ViewModels;

public class AdminCreateInformationRequestViewModel
{
    [Required]
    [EmailAddress]
    [StringLength(256, ErrorMessage = "Partner email cannot exceed 256 characters.")]
    [Display(Name = "Partner email")]
    public string PartnerEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(TextInputLimits.MaxPlaintextLength, ErrorMessage = "Instructions cannot exceed 10000 characters.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Instructions")]
    public string RequestInstructions { get; set; } = string.Empty;

    [Range(1, 168)]
    [Display(Name = "Expiration")]
    public int ExpiryHours { get; set; } = 4;

    [Display(Name = "Require Microsoft Entra ID sign-in")]
    public bool RequireOidcLogin { get; set; }

    public bool IsOidcLoginRequirementAvailable { get; set; }
}

public class AdminInformationRequestCreatedViewModel
{
    public Guid RequestId { get; set; }
    public string PartnerEmail { get; set; } = string.Empty;
    public string RequestLink { get; set; } = string.Empty;
    public string AccessCode { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool RequireOidcLogin { get; set; }
}

public class AdminInformationRequestDashboardViewModel
{
    public string? ErrorMessage { get; set; }
    public string Search { get; set; } = string.Empty;
    public string SelectedStatus { get; set; } = AdminInformationRequestStatusOption.All;
    public int ActiveCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int SubmittedCount { get; set; }
    public int RevokedCount { get; set; }
    public int TotalVisibleRequests { get; set; }
    public IReadOnlyList<AdminInformationRequestListItemViewModel> Requests { get; set; } = Array.Empty<AdminInformationRequestListItemViewModel>();

    public bool HasRequests => TotalVisibleRequests > 0;
    public bool HasResults => Requests.Count > 0;
    public bool HasFilters => !string.IsNullOrWhiteSpace(Search)
        || !string.Equals(SelectedStatus, AdminInformationRequestStatusOption.All, StringComparison.Ordinal);
}

public class AdminInformationRequestListItemViewModel
{
    public Guid Id { get; set; }
    public string PartnerEmail { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastSubmittedAtUtc { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
    public bool RequireOidcLogin { get; set; }

    public bool HasResponse => LastSubmittedAtUtc.HasValue;
    public string AccessModeLabel => RequireOidcLogin ? "Microsoft Entra ID + email + code" : "Email + code";
    public string AccessModeTone => RequireOidcLogin ? "entra" : "standard";
    public string StatusLabel => IsExpired ? "Expired" : IsExpiringSoon ? "Expiring soon" : HasResponse ? "Response received" : "Awaiting response";
    public string StatusTone => IsExpired ? "expired" : IsExpiringSoon ? "warning" : HasResponse ? "accessed" : "active";
}

public class AdminInformationRequestDetailsViewModel
{
    public Guid Id { get; set; }
    public string PartnerEmail { get; set; } = string.Empty;
    public string RequestInstructions { get; set; } = string.Empty;
    public string PartnerResponse { get; set; } = string.Empty;
    public string ResponseEncryptionMode { get; set; } = SecretEncryptionModes.ServerManaged;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastSubmittedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool RequireOidcLogin { get; set; }
    public bool IsExpired { get; set; }
    public int ExtendHours { get; set; } = 4;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public bool HasResponse => !string.IsNullOrWhiteSpace(PartnerResponse);
    public bool IsClientEncrypted => SecretEncryptionModes.IsClientEncrypted(ResponseEncryptionMode);
}

public static class AdminInformationRequestStatusOption
{
    public const string All = "all";
    public const string Active = "active";
    public const string ExpiringSoon = "expiring-soon";
    public const string Submitted = "submitted";
    public const string Expired = "expired";

    public static readonly IReadOnlyList<string> AllValues =
    [
        All,
        Active,
        ExpiringSoon,
        Submitted,
        Expired
    ];

    public static string Normalize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? All : value.Trim().ToLowerInvariant();
        return AllValues.Contains(normalized, StringComparer.Ordinal) ? normalized : All;
    }

    public static string GetLabel(string value)
    {
        return Normalize(value) switch
        {
            Active => "Active",
            ExpiringSoon => "Expiring soon",
            Submitted => "Response received",
            Expired => "Expired",
            _ => "All statuses"
        };
    }
}
