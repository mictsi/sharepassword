using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using SharePassword.Models;
using SharePassword.Options;
using SharePassword.Services;
using SharePassword.ViewModels;

namespace SharePassword.Controllers;

public class InformationRequestController : Controller
{
    private readonly IInformationRequestStore _informationRequestStore;
    private readonly IAccessCodeService _accessCodeService;
    private readonly IPasswordCryptoService _passwordCryptoService;
    private readonly IAuditLogger _auditLogger;
    private readonly IApplicationTime _applicationTime;
    private readonly ISystemConfigurationService _systemConfigurationService;
    private readonly IUsageMetricsService _usageMetricsService;
    private readonly OidcAuthOptions _oidcAuthOptions;

    public InformationRequestController(
        IInformationRequestStore informationRequestStore,
        IAccessCodeService accessCodeService,
        IPasswordCryptoService passwordCryptoService,
        IAuditLogger auditLogger,
        IApplicationTime applicationTime,
        ISystemConfigurationService systemConfigurationService,
        IUsageMetricsService usageMetricsService,
        IOptions<OidcAuthOptions> oidcAuthOptions)
    {
        _informationRequestStore = informationRequestStore;
        _accessCodeService = accessCodeService;
        _passwordCryptoService = passwordCryptoService;
        _auditLogger = auditLogger;
        _applicationTime = applicationTime;
        _systemConfigurationService = systemConfigurationService;
        _usageMetricsService = usageMetricsService;
        _oidcAuthOptions = oidcAuthOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Access(string token)
    {
        token = (token ?? string.Empty).Trim();
        if (!IsValidToken(token))
        {
            return BadRequest();
        }

        InformationRequest? request;
        try
        {
            request = await _informationRequestStore.GetInformationRequestByTokenAsync(token);
        }
        catch (DatabaseOperationException exception)
        {
            var unavailableModel = new InformationRequestAccessViewModel { Token = token };
            ModelState.AddModelError(string.Empty, exception.UserMessage);
            return View(unavailableModel);
        }

        var model = BuildAccessModel(token, request);

        if (request?.RequireOidcLogin == true)
        {
            if (!_oidcAuthOptions.Enabled)
            {
                return Forbid();
            }

            if (User.Identity?.IsAuthenticated != true)
            {
                return Challenge(new AuthenticationProperties
                {
                    RedirectUri = ApplicationPathHelper.BuildAppPath(Request.PathBase, Url.Action(nameof(Access), new { token }) ?? "/")
                }, OpenIdConnectDefaults.AuthenticationScheme);
            }

            var oidcEmail = GetAuthenticatedEmail();
            if (string.IsNullOrWhiteSpace(oidcEmail))
            {
                await _auditLogger.LogAsync("oidc-user", "unknown", "information-request.access", false, details: "Microsoft Entra ID-authenticated user has no usable email claim.");
                return Forbid();
            }

            if (!string.Equals(request.PartnerEmail, oidcEmail, StringComparison.OrdinalIgnoreCase))
            {
                await _auditLogger.LogAsync("oidc-user", oidcEmail, "information-request.access", false, "InformationRequest", request.Id.ToString(), "OIDC partner mismatch.");
                return Forbid();
            }

            model.Email = oidcEmail;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Access(InformationRequestAccessViewModel model)
    {
        model.Token = (model.Token ?? string.Empty).Trim();
        model.Code = (model.Code ?? string.Empty).Trim();
        model.Email = (model.Email ?? string.Empty).Trim().ToLowerInvariant();

        var validation = await ValidatePartnerAccessAsync(model.Token, model.Email, model.Code, model, "Access");
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        return View("Response", BuildResponseModel(validation.Request!, validation.Email, model.Code));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(InformationRequestResponseViewModel model)
    {
        model.Token = (model.Token ?? string.Empty).Trim();
        model.Code = (model.Code ?? string.Empty).Trim();
        model.Email = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
        model.PartnerResponse = (model.PartnerResponse ?? string.Empty).Replace("\0", string.Empty);
        model.ClientEncryptedPartnerResponsePayload = (model.ClientEncryptedPartnerResponsePayload ?? string.Empty).Trim();

        var validation = await ValidatePartnerAccessAsync(model.Token, model.Email, model.Code, model, "Response");
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var request = validation.Request!;
        ValidateResponsePayload(model);

        if (!ModelState.IsValid)
        {
            var redisplayModel = BuildResponseModel(request, validation.Email, model.Code);
            redisplayModel.PartnerResponse = model.PartnerResponse;
            redisplayModel.UseClientEncryption = model.UseClientEncryption;
            return View("Response", redisplayModel);
        }

        request.ResponseEncryptionMode = model.UseClientEncryption
            ? SecretEncryptionModes.ClientAesGcm
            : SecretEncryptionModes.ServerManaged;
        request.EncryptedPartnerResponse = model.UseClientEncryption
            ? model.ClientEncryptedPartnerResponsePayload
            : _passwordCryptoService.Encrypt(model.PartnerResponse);
        request.LastSubmittedAtUtc = _applicationTime.UtcNow;
        request.FailedAccessAttempts = 0;
        request.AccessPausedUntilUtc = null;

        try
        {
            await _informationRequestStore.UpsertInformationRequestAsync(request);
        }
        catch (DatabaseOperationException exception)
        {
            await _auditLogger.LogAsync("external-user", validation.Email, "information-request.response.update", false, "InformationRequest", request.Id.ToString(), exception.DiagnosticMessage);
            ModelState.AddModelError(string.Empty, exception.UserMessage);
            var redisplayModel = BuildResponseModel(request, validation.Email, model.Code);
            redisplayModel.PartnerResponse = model.PartnerResponse;
            redisplayModel.UseClientEncryption = model.UseClientEncryption;
            return View("Response", redisplayModel);
        }

        await _auditLogger.LogAsync("external-user", validation.Email, "information-request.response.update", true, "InformationRequest", request.Id.ToString(), $"Response updated. responseEncryptionMode={request.ResponseEncryptionMode}");
        await _usageMetricsService.RecordAsync(DbUsageMetricsService.InformationRequestResponseSubmittedKey, "external-user", validation.Email, relatedId: request.Id.ToString(), details: "Information request response updated.");

        return View("Response", BuildResponseModel(request, validation.Email, model.Code, "Information saved."));
    }

    private async Task<PartnerAccessValidation> ValidatePartnerAccessAsync(string token, string email, string code, object viewModel, string viewName)
    {
        if (!IsValidToken(token))
        {
            return PartnerAccessValidation.Failed(BadRequest());
        }

        InformationRequest? request;
        try
        {
            request = await _informationRequestStore.GetInformationRequestByTokenAsync(token);
        }
        catch (DatabaseOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.UserMessage);
            return PartnerAccessValidation.Failed(View(viewName, viewModel));
        }

        SetRequireOidcLogin(viewModel, request?.RequireOidcLogin ?? false);

        if (request is null)
        {
            await _auditLogger.LogAsync("external-user", GetAuditIdentifier(email), "information-request.access", false, details: "Unknown token.");
            ModelState.AddModelError(string.Empty, "Invalid link or access details.");
            return PartnerAccessValidation.Failed(View(viewName, viewModel));
        }

        if (request.ExpiresAtUtc <= _applicationTime.UtcNow)
        {
            try
            {
                await _informationRequestStore.DeleteInformationRequestAsync(request.Id);
            }
            catch (DatabaseOperationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.UserMessage);
                return PartnerAccessValidation.Failed(View(viewName, viewModel));
            }

            await _auditLogger.LogAsync("external-user", GetAuditIdentifier(email), "information-request.access", false, "InformationRequest", request.Id.ToString(), "Information request expired.");
            ModelState.AddModelError(string.Empty, "This information request has expired.");
            return PartnerAccessValidation.Failed(View(viewName, viewModel));
        }

        SetInformationRequestAccessSummary(viewModel, request);

        if (request.RequireOidcLogin)
        {
            if (!_oidcAuthOptions.Enabled)
            {
                return PartnerAccessValidation.Failed(Forbid());
            }

            if (User.Identity?.IsAuthenticated != true)
            {
                return PartnerAccessValidation.Failed(Challenge(new AuthenticationProperties
                {
                    RedirectUri = ApplicationPathHelper.BuildAppPath(Request.PathBase, Url.Action(nameof(Access), new { token }) ?? "/")
                }, OpenIdConnectDefaults.AuthenticationScheme));
            }

            var oidcEmail = GetAuthenticatedEmail();
            if (string.IsNullOrWhiteSpace(oidcEmail))
            {
                await _auditLogger.LogAsync("oidc-user", "unknown", "information-request.access", false, details: "Microsoft Entra ID-authenticated user has no usable email claim.");
                ModelState.AddModelError(string.Empty, "Unable to resolve your Microsoft Entra ID email from token claims.");
                return PartnerAccessValidation.Failed(View(viewName, viewModel));
            }

            email = oidcEmail;
            SetEmail(viewModel, oidcEmail);

            if (!string.Equals(request.PartnerEmail, email, StringComparison.OrdinalIgnoreCase))
            {
                await _auditLogger.LogAsync("oidc-user", email, "information-request.access", false, "InformationRequest", request.Id.ToString(), "OIDC partner mismatch.");
                return PartnerAccessValidation.Failed(Forbid());
            }
        }
        else if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError("Email", "Email address is required.");
            return PartnerAccessValidation.Failed(View(viewName, viewModel));
        }

        var pausedResult = await EnforceInformationRequestAccessPauseAsync(request, viewModel, viewName, email);
        if (pausedResult is not null)
        {
            return PartnerAccessValidation.Failed(pausedResult);
        }

        if (!string.Equals(request.PartnerEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return PartnerAccessValidation.Failed(await RecordFailedInformationRequestAccessAsync(request, viewModel, viewName, email, "Email mismatch."));
        }

        if (!InformationRequestAccessCodeFormat.IsValid(code))
        {
            ModelState.AddModelError("Code", InformationRequestAccessCodeFormat.InvalidFormatErrorMessage);
            return PartnerAccessValidation.Failed(await RecordFailedInformationRequestAccessAsync(request, viewModel, viewName, email, "Invalid access code format."));
        }

        if (!_accessCodeService.Verify(code, request.AccessCodeHash))
        {
            return PartnerAccessValidation.Failed(await RecordFailedInformationRequestAccessAsync(request, viewModel, viewName, email, "Access code mismatch."));
        }

        request.FailedAccessAttempts = 0;
        request.AccessPausedUntilUtc = null;
        try
        {
            await _informationRequestStore.UpsertInformationRequestAsync(request);
        }
        catch (DatabaseOperationException exception)
        {
            await _auditLogger.LogAsync("external-user", email, "information-request.access", false, "InformationRequest", request.Id.ToString(), exception.DiagnosticMessage);
            ModelState.AddModelError(string.Empty, exception.UserMessage);
            return PartnerAccessValidation.Failed(View(viewName, viewModel));
        }

        await _auditLogger.LogAsync("external-user", email, "information-request.access", true, "InformationRequest", request.Id.ToString());
        return PartnerAccessValidation.Success(request, email);
    }

    private static InformationRequestAccessViewModel BuildAccessModel(string token, InformationRequest? request)
    {
        return new InformationRequestAccessViewModel
        {
            Token = token,
            RequireOidcLogin = request?.RequireOidcLogin ?? false,
            HasSubmittedResponse = HasSubmittedResponse(request),
            ExpiresAtUtc = request?.ExpiresAtUtc,
            LastSubmittedAtUtc = request?.LastSubmittedAtUtc
        };
    }

    private static void SetInformationRequestAccessSummary(object viewModel, InformationRequest request)
    {
        if (viewModel is not InformationRequestAccessViewModel accessModel)
        {
            return;
        }

        accessModel.HasSubmittedResponse = HasSubmittedResponse(request);
        accessModel.ExpiresAtUtc = request.ExpiresAtUtc;
        accessModel.LastSubmittedAtUtc = request.LastSubmittedAtUtc;
    }

    private static bool HasSubmittedResponse(InformationRequest? request)
    {
        return request?.LastSubmittedAtUtc is not null
            && !string.IsNullOrWhiteSpace(request.EncryptedPartnerResponse);
    }

    private InformationRequestResponseViewModel BuildResponseModel(InformationRequest request, string email, string code, string? successMessage = null)
    {
        var responseEncryptionMode = SecretEncryptionModes.Normalize(request.ResponseEncryptionMode);
        var isClientEncrypted = SecretEncryptionModes.IsClientEncrypted(responseEncryptionMode);
        var responsePayload = string.Empty;
        var encryptedPayload = string.Empty;

        if (!string.IsNullOrWhiteSpace(request.EncryptedPartnerResponse))
        {
            if (isClientEncrypted)
            {
                encryptedPayload = request.EncryptedPartnerResponse;
            }
            else
            {
                responsePayload = _passwordCryptoService.Decrypt(request.EncryptedPartnerResponse);
            }
        }

        return new InformationRequestResponseViewModel
        {
            RequestId = request.Id,
            Token = request.AccessToken,
            Email = email,
            Code = code,
            RequestInstructions = request.RequestInstructions,
            PartnerResponse = responsePayload,
            UseClientEncryption = isClientEncrypted,
            ExistingClientEncryptedPartnerResponsePayload = encryptedPayload,
            ResponseEncryptionMode = responseEncryptionMode,
            ExpiresAtUtc = request.ExpiresAtUtc,
            LastSubmittedAtUtc = request.LastSubmittedAtUtc,
            SuccessMessage = successMessage,
            RequireOidcLogin = request.RequireOidcLogin
        };
    }

    private void ValidateResponsePayload(InformationRequestResponseViewModel model)
    {
        if (model.UseClientEncryption)
        {
            ModelState.Remove(nameof(model.PartnerResponse));

            if (!ClientEncryptedSecretPayload.TryValidate(model.ClientEncryptedPartnerResponsePayload, out var errorMessage))
            {
                ModelState.AddModelError(nameof(model.ClientEncryptedPartnerResponsePayload), errorMessage);
                ModelState.AddModelError(string.Empty, "The response must be encrypted in your browser before it can be saved.");
            }

            return;
        }

        model.ClientEncryptedPartnerResponsePayload = string.Empty;
        ModelState.Remove(nameof(model.ClientEncryptedPartnerResponsePayload));
        if (string.IsNullOrWhiteSpace(model.PartnerResponse))
        {
            ModelState.AddModelError(nameof(model.PartnerResponse), "Information is required.");
        }
    }

    private async Task<IActionResult?> EnforceInformationRequestAccessPauseAsync(InformationRequest request, object model, string viewName, string email)
    {
        if (request.AccessPausedUntilUtc is not { } pausedUntilUtc)
        {
            return null;
        }

        var utcNow = _applicationTime.UtcNow;
        if (pausedUntilUtc > utcNow)
        {
            await _auditLogger.LogAsync("external-user", GetAuditIdentifier(email), "information-request.access", false, "InformationRequest", request.Id.ToString(), "Information request access paused after failed attempts.");
            ModelState.AddModelError(string.Empty, BuildPausedMessage(pausedUntilUtc, utcNow));
            return View(viewName, model);
        }

        request.FailedAccessAttempts = 0;
        request.AccessPausedUntilUtc = null;
        try
        {
            await _informationRequestStore.UpsertInformationRequestAsync(request);
        }
        catch (DatabaseOperationException exception)
        {
            await _auditLogger.LogAsync("external-user", GetAuditIdentifier(email), "information-request.access", false, "InformationRequest", request.Id.ToString(), exception.DiagnosticMessage);
            ModelState.AddModelError(string.Empty, exception.UserMessage);
            return View(viewName, model);
        }

        return null;
    }

    private async Task<IActionResult> RecordFailedInformationRequestAccessAsync(InformationRequest request, object model, string viewName, string email, string details)
    {
        SystemConfiguration configuration;
        try
        {
            configuration = await _systemConfigurationService.GetConfigurationAsync();
        }
        catch (DatabaseOperationException exception)
        {
            await _auditLogger.LogAsync("external-user", GetAuditIdentifier(email), "information-request.access", false, "InformationRequest", request.Id.ToString(), exception.DiagnosticMessage);
            ModelState.AddModelError(string.Empty, exception.UserMessage);
            return View(viewName, model);
        }

        var failedAttemptLimit = Math.Max(1, configuration.ShareAccessFailedAttemptLimit);
        var pauseMinutes = Math.Max(1, configuration.ShareAccessPauseMinutes);
        var utcNow = _applicationTime.UtcNow;

        request.FailedAccessAttempts = Math.Max(0, request.FailedAccessAttempts) + 1;
        if (request.FailedAccessAttempts >= failedAttemptLimit)
        {
            request.AccessPausedUntilUtc = utcNow.AddMinutes(pauseMinutes);
        }

        try
        {
            await _informationRequestStore.UpsertInformationRequestAsync(request);
        }
        catch (DatabaseOperationException exception)
        {
            await _auditLogger.LogAsync("external-user", GetAuditIdentifier(email), "information-request.access", false, "InformationRequest", request.Id.ToString(), exception.DiagnosticMessage);
            ModelState.AddModelError(string.Empty, exception.UserMessage);
            return View(viewName, model);
        }

        var auditDetails = request.AccessPausedUntilUtc is null
            ? details
            : $"{details} Information request access paused after {request.FailedAccessAttempts} failed attempts.";
        await _auditLogger.LogAsync("external-user", GetAuditIdentifier(email), "information-request.access", false, "InformationRequest", request.Id.ToString(), auditDetails);

        if (request.AccessPausedUntilUtc is { } pausedUntilUtc)
        {
            ModelState.AddModelError(string.Empty, BuildPausedMessage(pausedUntilUtc, utcNow));
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Invalid link or access details.");
        }

        return View(viewName, model);
    }

    private string GetAuthenticatedEmail()
    {
        return User.FindFirstValue("preferred_username")?.Trim().ToLowerInvariant()
               ?? User.FindFirstValue("email")?.Trim().ToLowerInvariant()
               ?? User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant()
               ?? User.FindFirstValue("upn")?.Trim().ToLowerInvariant()
               ?? User.FindFirstValue("unique_name")?.Trim().ToLowerInvariant()
               ?? string.Empty;
    }

    private static void SetRequireOidcLogin(object viewModel, bool value)
    {
        switch (viewModel)
        {
            case InformationRequestAccessViewModel accessModel:
                accessModel.RequireOidcLogin = value;
                break;
            case InformationRequestResponseViewModel responseModel:
                responseModel.RequireOidcLogin = value;
                break;
        }
    }

    private static void SetEmail(object viewModel, string email)
    {
        switch (viewModel)
        {
            case InformationRequestAccessViewModel accessModel:
                accessModel.Email = email;
                break;
            case InformationRequestResponseViewModel responseModel:
                responseModel.Email = email;
                break;
        }
    }

    private static string BuildPausedMessage(DateTime pausedUntilUtc, DateTime utcNow)
    {
        var remainingMinutes = Math.Max(1, (int)Math.Ceiling((pausedUntilUtc - utcNow).TotalMinutes));
        return $"Too many failed attempts for this information request. Try again in {remainingMinutes} minute{(remainingMinutes == 1 ? string.Empty : "s")}.";
    }

    private static string GetAuditIdentifier(string email)
    {
        return string.IsNullOrWhiteSpace(email) ? "unknown" : email;
    }

    private static bool IsValidToken(string token)
    {
        return token.Length == 32 && token.All(Uri.IsHexDigit);
    }

    private sealed class PartnerAccessValidation
    {
        public InformationRequest? Request { get; private init; }
        public string Email { get; private init; } = string.Empty;
        public IActionResult? Result { get; private init; }

        public static PartnerAccessValidation Success(InformationRequest request, string email)
        {
            return new PartnerAccessValidation { Request = request, Email = email };
        }

        public static PartnerAccessValidation Failed(IActionResult result)
        {
            return new PartnerAccessValidation { Result = result };
        }
    }
}
