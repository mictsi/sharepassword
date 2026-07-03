using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using SharePassword.Models;
using SharePassword.Options;
using SharePassword.Services;
using SharePassword.ViewModels;

namespace SharePassword.Controllers;

[Authorize(Policy = "UserOrAdmin")]
public class InformationRequestsController : Controller
{
    private readonly IInformationRequestStore _informationRequestStore;
    private readonly IAuditLogReader _auditLogReader;
    private readonly IPasswordCryptoService _passwordCryptoService;
    private readonly IAccessCodeService _accessCodeService;
    private readonly IAuditLogger _auditLogger;
    private readonly IApplicationTime _applicationTime;
    private readonly IUsageMetricsService _usageMetricsService;
    private readonly ShareOptions _shareOptions;
    private readonly OidcAuthOptions _oidcAuthOptions;
    private readonly string _adminRoleName;

    public InformationRequestsController(
        IInformationRequestStore informationRequestStore,
        IAuditLogReader auditLogReader,
        IPasswordCryptoService passwordCryptoService,
        IAccessCodeService accessCodeService,
        IAuditLogger auditLogger,
        IApplicationTime applicationTime,
        IUsageMetricsService usageMetricsService,
        IOptions<ShareOptions> shareOptions,
        IOptions<OidcAuthOptions> oidcAuthOptions)
    {
        _informationRequestStore = informationRequestStore;
        _auditLogReader = auditLogReader;
        _passwordCryptoService = passwordCryptoService;
        _accessCodeService = accessCodeService;
        _auditLogger = auditLogger;
        _applicationTime = applicationTime;
        _usageMetricsService = usageMetricsService;
        _shareOptions = shareOptions.Value;
        _oidcAuthOptions = oidcAuthOptions.Value;
        _adminRoleName = string.IsNullOrWhiteSpace(_oidcAuthOptions.AdminRoleName) ? "Admin" : _oidcAuthOptions.AdminRoleName.Trim();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status = null)
    {
        var normalizedSearch = NormalizeFilter(search) ?? string.Empty;
        var normalizedStatus = AdminInformationRequestStatusOption.Normalize(status);

        try
        {
            var requests = await _informationRequestStore.GetAllInformationRequestsAsync();
            var nowUtc = _applicationTime.UtcNow;
            var currentUser = GetCurrentUserIdentifier();
            var isAdmin = User.IsInRole(_adminRoleName);

            if (!isAdmin)
            {
                requests = requests
                    .Where(x => string.Equals(x.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var allItems = requests
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new AdminInformationRequestListItemViewModel
                {
                    Id = x.Id,
                    PartnerEmail = x.PartnerEmail,
                    CreatedBy = x.CreatedBy,
                    CreatedAtUtc = x.CreatedAtUtc,
                    ExpiresAtUtc = x.ExpiresAtUtc,
                    LastSubmittedAtUtc = x.LastSubmittedAtUtc,
                    IsExpired = x.ExpiresAtUtc <= nowUtc,
                    IsExpiringSoon = x.ExpiresAtUtc > nowUtc && x.ExpiresAtUtc <= nowUtc.AddHours(24),
                    RequireOidcLogin = x.RequireOidcLogin
                })
                .ToList();

            var filteredItems = allItems
                .Where(x => MatchesDashboardSearch(x, normalizedSearch) && MatchesDashboardStatus(x, normalizedStatus))
                .ToList();

            var auditLogs = await _auditLogReader.GetLatestAsync(5000);
            var visibleAuditLogs = isAdmin
                ? auditLogs
                : auditLogs.Where(x => string.Equals(x.ActorIdentifier, currentUser, StringComparison.OrdinalIgnoreCase)).ToList();

            var model = new AdminInformationRequestDashboardViewModel
            {
                ErrorMessage = TempData["ErrorMessage"]?.ToString(),
                Search = normalizedSearch,
                SelectedStatus = normalizedStatus,
                ActiveCount = allItems.Count(x => !x.IsExpired),
                ExpiringSoonCount = allItems.Count(x => x.IsExpiringSoon),
                SubmittedCount = allItems.Count(x => x.HasResponse),
                RevokedCount = visibleAuditLogs.Count(x => x.Success && string.Equals(x.Operation, "information-request.revoke", StringComparison.OrdinalIgnoreCase)),
                TotalVisibleRequests = allItems.Count,
                Requests = filteredItems
            };

            return View(model);
        }
        catch (DatabaseOperationException exception)
        {
            return View(new AdminInformationRequestDashboardViewModel
            {
                ErrorMessage = exception.UserMessage,
                Search = normalizedSearch,
                SelectedStatus = normalizedStatus
            });
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(BuildCreateModel(new AdminCreateInformationRequestViewModel { ExpiryHours = _shareOptions.DefaultExpiryHours }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminCreateInformationRequestViewModel model)
    {
        model.PartnerEmail = (model.PartnerEmail ?? string.Empty).Trim();
        model.RequestInstructions = (model.RequestInstructions ?? string.Empty).Replace("\0", string.Empty);

        if (model.RequireOidcLogin && !_oidcAuthOptions.Enabled)
        {
            ModelState.AddModelError(nameof(model.RequireOidcLogin), "Microsoft Entra ID sign-in must be enabled before requiring it for request links.");
        }

        BuildCreateModel(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var accessCode = _accessCodeService.GenerateCode(InformationRequestAccessCodeFormat.Length);
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        var now = _applicationTime.UtcNow;
        var actorIdentifier = GetCurrentUserIdentifier();
        var actorType = GetCurrentActorType();

        var request = new InformationRequest
        {
            Id = Guid.NewGuid(),
            PartnerEmail = model.PartnerEmail.Trim().ToLowerInvariant(),
            RequestInstructions = model.RequestInstructions,
            EncryptedPartnerResponse = string.Empty,
            ResponseEncryptionMode = SecretEncryptionModes.ServerManaged,
            AccessCodeHash = _accessCodeService.HashCode(accessCode),
            AccessToken = token,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(model.ExpiryHours),
            CreatedBy = actorIdentifier,
            RequireOidcLogin = model.RequireOidcLogin
        };

        try
        {
            await _informationRequestStore.UpsertInformationRequestAsync(request);
        }
        catch (DatabaseOperationException exception)
        {
            await _auditLogger.LogAsync(
                actorType,
                actorIdentifier,
                "information-request.create",
                false,
                targetType: "InformationRequest",
                targetId: request.Id.ToString(),
                details: exception.DiagnosticMessage);

            ModelState.AddModelError(string.Empty, exception.UserMessage);
            return View(model);
        }

        await _auditLogger.LogAsync(
            actorType,
            actorIdentifier,
            "information-request.create",
            true,
            targetType: "InformationRequest",
            targetId: request.Id.ToString(),
            details: $"Created information request for {request.PartnerEmail} expiring at {_applicationTime.FormatUtcForDisplay(request.ExpiresAtUtc)} ({_applicationTime.TimeZoneId}). requireOidcLogin={request.RequireOidcLogin}");
        await _usageMetricsService.RecordAsync(DbUsageMetricsService.InformationRequestCreatedKey, actorType, actorIdentifier, relatedId: request.Id.ToString(), details: $"Information request created for {request.PartnerEmail}.");

        var link = ApplicationPathHelper.BuildAbsoluteAppUrl(Request, $"/r/{request.AccessToken}");

        return View("Created", new AdminInformationRequestCreatedViewModel
        {
            RequestId = request.Id,
            PartnerEmail = request.PartnerEmail,
            RequestLink = link,
            AccessCode = accessCode,
            ExpiresAtUtc = request.ExpiresAtUtc,
            RequireOidcLogin = request.RequireOidcLogin
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var actorIdentifier = GetCurrentUserIdentifier();
        try
        {
            var request = await _informationRequestStore.GetInformationRequestByIdAsync(id);
            if (request is null)
            {
                return NotFound();
            }

            if (!CanManageRequest(request, actorIdentifier))
            {
                await _auditLogger.LogAsync(GetCurrentActorType(), actorIdentifier, "information-request.details", false, "InformationRequest", id.ToString(), "User attempted to view a request they do not own.");
                return Forbid();
            }

            var model = BuildDetailsModel(request);
            model.ErrorMessage = TempData["ErrorMessage"]?.ToString();
            model.SuccessMessage = TempData["SuccessMessage"]?.ToString();
            return View(model);
        }
        catch (DatabaseOperationException exception)
        {
            TempData["ErrorMessage"] = exception.UserMessage;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Extend(Guid id, int extendHours)
    {
        extendHours = Math.Clamp(extendHours, 1, 168);
        var actorIdentifier = GetCurrentUserIdentifier();
        var actorType = GetCurrentActorType();

        try
        {
            var request = await _informationRequestStore.GetInformationRequestByIdAsync(id);
            if (request is null)
            {
                await _auditLogger.LogAsync(actorType, actorIdentifier, "information-request.extend", false, "InformationRequest", id.ToString(), "Information request not found.");
                return NotFound();
            }

            if (!CanManageRequest(request, actorIdentifier))
            {
                await _auditLogger.LogAsync(actorType, actorIdentifier, "information-request.extend", false, "InformationRequest", id.ToString(), "User attempted to extend a request they do not own.");
                return Forbid();
            }

            var nowUtc = _applicationTime.UtcNow;
            var baseUtc = request.ExpiresAtUtc > nowUtc ? request.ExpiresAtUtc : nowUtc;
            request.ExpiresAtUtc = baseUtc.AddHours(extendHours);

            await _informationRequestStore.UpsertInformationRequestAsync(request);
            await _auditLogger.LogAsync(actorType, actorIdentifier, "information-request.extend", true, "InformationRequest", id.ToString(), $"Extended request expiration by {extendHours} hours to {_applicationTime.FormatUtcForDisplay(request.ExpiresAtUtc)} ({_applicationTime.TimeZoneId}).");

            TempData["SuccessMessage"] = "Information request expiration extended.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DatabaseOperationException exception)
        {
            await _auditLogger.LogAsync(actorType, actorIdentifier, "information-request.extend", false, "InformationRequest", id.ToString(), exception.DiagnosticMessage);
            TempData["ErrorMessage"] = exception.UserMessage;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var actorIdentifier = GetCurrentUserIdentifier();
        var actorType = GetCurrentActorType();

        try
        {
            var request = await _informationRequestStore.GetInformationRequestByIdAsync(id);
            if (request is null)
            {
                await _auditLogger.LogAsync(actorType, actorIdentifier, "information-request.revoke", false, "InformationRequest", id.ToString(), "Information request not found.");
                return NotFound();
            }

            if (!CanManageRequest(request, actorIdentifier))
            {
                await _auditLogger.LogAsync(actorType, actorIdentifier, "information-request.revoke", false, "InformationRequest", id.ToString(), "User attempted to revoke a request they do not own.");
                return Forbid();
            }

            await _informationRequestStore.DeleteInformationRequestAsync(id);
            await _auditLogger.LogAsync(actorType, actorIdentifier, "information-request.revoke", true, "InformationRequest", id.ToString());
            await _usageMetricsService.RecordAsync(DbUsageMetricsService.InformationRequestRevokedKey, actorType, actorIdentifier, relatedId: id.ToString(), details: "Information request revoked.");

            return RedirectToAction(nameof(Index));
        }
        catch (DatabaseOperationException exception)
        {
            await _auditLogger.LogAsync(actorType, actorIdentifier, "information-request.revoke", false, "InformationRequest", id.ToString(), exception.DiagnosticMessage);
            TempData["ErrorMessage"] = exception.UserMessage;
            return RedirectToAction(nameof(Index));
        }
    }

    private AdminCreateInformationRequestViewModel BuildCreateModel(AdminCreateInformationRequestViewModel model)
    {
        model.IsOidcLoginRequirementAvailable = _oidcAuthOptions.Enabled;
        if (!_oidcAuthOptions.Enabled)
        {
            model.RequireOidcLogin = false;
        }

        return model;
    }

    private AdminInformationRequestDetailsViewModel BuildDetailsModel(InformationRequest request)
    {
        var responseEncryptionMode = SecretEncryptionModes.Normalize(request.ResponseEncryptionMode);
        var responsePayload = string.Empty;

        if (!string.IsNullOrWhiteSpace(request.EncryptedPartnerResponse))
        {
            responsePayload = SecretEncryptionModes.IsClientEncrypted(responseEncryptionMode)
                ? request.EncryptedPartnerResponse
                : _passwordCryptoService.Decrypt(request.EncryptedPartnerResponse);
        }

        return new AdminInformationRequestDetailsViewModel
        {
            Id = request.Id,
            PartnerEmail = request.PartnerEmail,
            RequestInstructions = request.RequestInstructions,
            PartnerResponse = responsePayload,
            ResponseEncryptionMode = responseEncryptionMode,
            CreatedAtUtc = request.CreatedAtUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            LastSubmittedAtUtc = request.LastSubmittedAtUtc,
            CreatedBy = request.CreatedBy,
            RequireOidcLogin = request.RequireOidcLogin,
            IsExpired = request.ExpiresAtUtc <= _applicationTime.UtcNow,
            ExtendHours = _shareOptions.DefaultExpiryHours
        };
    }

    private bool CanManageRequest(InformationRequest request, string actorIdentifier)
    {
        return User.IsInRole(_adminRoleName)
            || string.Equals(request.CreatedBy, actorIdentifier, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? source, string value)
    {
        return !string.IsNullOrWhiteSpace(source)
            && source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDashboardSearch(AdminInformationRequestListItemViewModel item, string search)
    {
        return string.IsNullOrWhiteSpace(search)
            || Contains(item.PartnerEmail, search)
            || Contains(item.CreatedBy, search);
    }

    private static bool MatchesDashboardStatus(AdminInformationRequestListItemViewModel item, string status)
    {
        return AdminInformationRequestStatusOption.Normalize(status) switch
        {
            AdminInformationRequestStatusOption.Active => !item.IsExpired,
            AdminInformationRequestStatusOption.ExpiringSoon => item.IsExpiringSoon,
            AdminInformationRequestStatusOption.Submitted => item.HasResponse,
            AdminInformationRequestStatusOption.Expired => item.IsExpired,
            _ => true
        };
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string GetCurrentUserIdentifier()
    {
        return User.FindFirstValue("preferred_username")
               ?? User.FindFirstValue("email")
               ?? User.FindFirstValue("upn")
               ?? User.FindFirstValue("unique_name")
               ?? User.FindFirstValue(ClaimTypes.Name)
               ?? User.Identity?.Name
               ?? User.FindFirstValue(ClaimTypes.Email)
               ?? User.FindFirstValue("oid")
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? "unknown";
    }

    private string GetCurrentActorType()
    {
        return User.IsInRole(_adminRoleName) ? "admin" : "user";
    }
}

