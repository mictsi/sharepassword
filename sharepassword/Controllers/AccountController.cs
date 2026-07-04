using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SharePassword.Models;
using SharePassword.Options;
using SharePassword.Services;
using SharePassword.ViewModels;

namespace SharePassword.Controllers;

public class AccountController : Controller
{
    private readonly AdminAuthOptions _adminAuthOptions;
    private readonly OidcAuthOptions _oidcAuthOptions;
    private readonly IAuditLogger _auditLogger;
    private readonly ILocalUserService _localUserService;
    private readonly IUsageMetricsService _usageMetricsService;
    private readonly ILoginThrottleService _loginThrottleService;
    private readonly IApplicationTime _applicationTime;
    private readonly IPasskeyService _passkeyService;
    private readonly string _adminRoleName;
    private readonly string _userRoleName;

    private const string PasskeyRegistrationOptionsTempDataKey = "passkey.registration.options";
    private const string PasskeyAssertionOptionsTempDataKey = "passkey.assertion.options";

    public AccountController(
        IOptions<AdminAuthOptions> adminAuthOptions,
        IOptions<OidcAuthOptions> oidcAuthOptions,
        IAuditLogger auditLogger,
        ILocalUserService localUserService,
        IUsageMetricsService usageMetricsService,
        ILoginThrottleService loginThrottleService,
        IApplicationTime applicationTime,
        IPasskeyService passkeyService)
    {
        _adminAuthOptions = adminAuthOptions.Value;
        _oidcAuthOptions = oidcAuthOptions.Value;
        _auditLogger = auditLogger;
        _localUserService = localUserService;
        _usageMetricsService = usageMetricsService;
        _loginThrottleService = loginThrottleService;
        _applicationTime = applicationTime;
        _passkeyService = passkeyService;
        _adminRoleName = string.IsNullOrWhiteSpace(_oidcAuthOptions.AdminRoleName) ? "Admin" : _oidcAuthOptions.AdminRoleName.Trim();
        _userRoleName = string.IsNullOrWhiteSpace(_oidcAuthOptions.UserRoleName) ? "User" : _oidcAuthOptions.UserRoleName.Trim();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_oidcAuthOptions.Enabled && !IsLocalLoginAllowed())
        {
            return RedirectToAction(nameof(ExternalLogin), new { returnUrl });
        }

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["OidcEnabled"] = _oidcAuthOptions.Enabled;
        return View(new AdminLoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AdminLoginViewModel model, string? returnUrl = null)
    {
        if (_oidcAuthOptions.Enabled && !IsLocalLoginAllowed())
        {
            return RedirectToAction(nameof(ExternalLogin), new { returnUrl });
        }

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["OidcEnabled"] = _oidcAuthOptions.Enabled;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (_loginThrottleService.GetPauseExpiryUtc(model.Username) is { } pauseExpiryUtc)
        {
            await _auditLogger.LogAsync("admin", model.Username, "login.paused", false, details: "Sign-in attempt while account sign-in is paused after repeated failures.");
            ModelState.AddModelError(string.Empty, BuildLoginPausedMessage(pauseExpiryUtc));
            return View(model);
        }

        LocalUser? existingLocalUser = null;
        if (_localUserService.IsSupported)
        {
            try
            {
                existingLocalUser = await _localUserService.GetByUsernameAsync(model.Username);
            }
            catch (DatabaseOperationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.UserMessage);
                return View(model);
            }
        }

        if (existingLocalUser is not null)
        {
            var localAuthentication = await _localUserService.AuthenticateAsync(model.Username, model.Password);
            if (!localAuthentication.Succeeded || localAuthentication.User is null)
            {
                await _auditLogger.LogAsync("admin", model.Username, "local-user.login", false, details: localAuthentication.ErrorMessage ?? "Invalid username/password.");
                return await RecordFailedLoginAsync(model);
            }

            _loginThrottleService.RecordSuccess(model.Username);

            var hasTotp = HasConfirmedTotp(localAuthentication.User);
            var hasPasskeys = _passkeyService.IsEnabled && await _passkeyService.HasPasskeysAsync(localAuthentication.User.Id);

            if (localAuthentication.User.IsTotpRequired || hasTotp || hasPasskeys)
            {
                await SignInPendingTotpAsync(localAuthentication.User);

                if (hasTotp && hasPasskeys)
                {
                    return RedirectToAction(nameof(SecondFactor), new { returnUrl });
                }

                if (hasPasskeys)
                {
                    return RedirectToAction(nameof(PasskeyLogin), new { returnUrl });
                }

                if (hasTotp)
                {
                    return RedirectToAction(nameof(Totp), new { returnUrl });
                }

                // Nothing registered yet: let the user pick an enrollment method
                // when passkeys are available, otherwise enroll TOTP directly.
                return RedirectToAction(
                    _passkeyService.IsEnabled ? nameof(SecondFactorSetup) : nameof(TotpSetup),
                    new { returnUrl });
            }

            await CompleteLocalSignInAsync(localAuthentication.User);

            return RedirectToAction(nameof(PostLogin), new { returnUrl });
        }

        var validUsername = string.Equals(model.Username, _adminAuthOptions.Username, StringComparison.OrdinalIgnoreCase);
        var validPassword = AdminPasswordHash.Verify(model.Password, _adminAuthOptions.PasswordHash);

        if (!validUsername || !validPassword)
        {
            await _auditLogger.LogAsync("admin", model.Username, "admin.login", false, details: "Invalid username/password.");
            return await RecordFailedLoginAsync(model);
        }

        _loginThrottleService.RecordSuccess(model.Username);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _adminAuthOptions.Username),
            new(ClaimTypes.Role, _adminRoleName),
            new(ClaimTypes.Role, _userRoleName),
            new("auth_source", "config-admin")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });

        await _auditLogger.LogAsync("admin", _adminAuthOptions.Username, "admin.login", true);
        await _usageMetricsService.RecordAsync(DbUsageMetricsService.AdminLoginKey, "admin", _adminAuthOptions.Username, details: "Configured admin sign-in succeeded.");

        return RedirectToAction(nameof(PostLogin), new { returnUrl });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Totp(string? returnUrl = null)
    {
        var pendingUserId = GetPendingTotpUserId();
        if (pendingUserId is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var user = await _localUserService.GetByIdAsync(pendingUserId.Value);
        if (user is null || !HasConfirmedTotp(user))
        {
            return RedirectToAction(nameof(TotpSetup), new { returnUrl });
        }

        return View(new TotpVerificationViewModel { Username = user.Username });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Totp(TotpVerificationViewModel model, string? returnUrl = null)
    {
        var pendingUserId = GetPendingTotpUserId();
        if (pendingUserId is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var user = await _localUserService.GetByIdAsync(pendingUserId.Value);
        if (user is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        model.Username = user.Username;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _localUserService.VerifyTotpAsync(user.Id, model.Code, user.Username);
        if (!result.Succeeded || result.User is null)
        {
            await _auditLogger.LogAsync("admin", user.Username, "local-user.totp.verify", false, targetType: "LocalUser", targetId: user.Id.ToString(), details: result.AuditDetails);
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid authenticator code.");
            return View(model);
        }

        await CompleteLocalSignInAsync(result.User);
        return RedirectToAction(nameof(PostLogin), new { returnUrl });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> SecondFactor(string? returnUrl = null)
    {
        var pendingUser = await GetPendingSecondFactorUserAsync();
        if (pendingUser is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var hasTotp = HasConfirmedTotp(pendingUser);
        var hasPasskeys = _passkeyService.IsEnabled && await _passkeyService.HasPasskeysAsync(pendingUser.Id);

        if (!hasTotp && !hasPasskeys)
        {
            return RedirectToAction(nameof(TotpSetup), new { returnUrl });
        }

        if (!hasPasskeys)
        {
            return RedirectToAction(nameof(Totp), new { returnUrl });
        }

        if (!hasTotp)
        {
            return RedirectToAction(nameof(PasskeyLogin), new { returnUrl });
        }

        return View(new SecondFactorViewModel
        {
            Username = pendingUser.Username,
            HasTotp = hasTotp,
            HasPasskeys = hasPasskeys,
            ReturnUrl = returnUrl
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> SecondFactorSetup(string? returnUrl = null)
    {
        var pendingUser = await GetPendingSecondFactorUserAsync();
        if (pendingUser is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (!_passkeyService.IsEnabled)
        {
            return RedirectToAction(nameof(TotpSetup), new { returnUrl });
        }

        return View(new SecondFactorSetupViewModel
        {
            Username = pendingUser.Username,
            ReturnUrl = returnUrl
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> PasskeySetup(string? returnUrl = null)
    {
        var pendingUser = await GetPendingSecondFactorUserAsync();
        if (pendingUser is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (!_passkeyService.IsEnabled)
        {
            return RedirectToAction(nameof(TotpSetup), new { returnUrl });
        }

        return View(new PasskeySetupViewModel
        {
            Username = pendingUser.Username,
            ReturnUrl = returnUrl
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> PasskeyLogin(string? returnUrl = null)
    {
        var pendingUser = await GetPendingSecondFactorUserAsync();
        if (pendingUser is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (!_passkeyService.IsEnabled || !await _passkeyService.HasPasskeysAsync(pendingUser.Id))
        {
            return RedirectToAction(nameof(Totp), new { returnUrl });
        }

        return View(new PasskeyLoginViewModel
        {
            Username = pendingUser.Username,
            HasTotp = HasConfirmedTotp(pendingUser),
            ReturnUrl = returnUrl
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasskeyAssertionOptions()
    {
        var pendingUser = await GetPendingSecondFactorUserAsync();
        if (pendingUser is null || !_passkeyService.IsEnabled)
        {
            return Forbid();
        }

        if (_loginThrottleService.GetPauseExpiryUtc(pendingUser.Username) is { } pauseExpiryUtc)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, BuildLoginPausedMessage(pauseExpiryUtc));
        }

        var optionsJson = await _passkeyService.BeginAssertionAsync(pendingUser.Id);
        if (optionsJson is null)
        {
            return NotFound();
        }

        TempData[PasskeyAssertionOptionsTempDataKey] = optionsJson;
        return Content(optionsJson, "application/json");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasskeyAssertionVerify([FromBody] PasskeyAssertionVerifyRequest request)
    {
        var pendingUser = await GetPendingSecondFactorUserAsync();
        if (pendingUser is null || !_passkeyService.IsEnabled)
        {
            return Forbid();
        }

        if (_loginThrottleService.GetPauseExpiryUtc(pendingUser.Username) is { } pauseExpiryUtc)
        {
            await _auditLogger.LogAsync("admin", pendingUser.Username, "login.paused", false, details: "Passkey sign-in attempt while account sign-in is paused.");
            return Json(new { succeeded = false, error = BuildLoginPausedMessage(pauseExpiryUtc) });
        }

        if (TempData[PasskeyAssertionOptionsTempDataKey] is not string optionsJson)
        {
            return Json(new { succeeded = false, error = "The passkey sign-in session expired. Try again." });
        }

        var result = await _passkeyService.CompleteAssertionAsync(pendingUser.Id, optionsJson, request.Response ?? string.Empty);
        if (!result.Succeeded)
        {
            await _auditLogger.LogAsync("admin", pendingUser.Username, "local-user.passkey.login", false, targetType: "LocalUser", targetId: pendingUser.Id.ToString(), details: result.ErrorMessage);

            if (_loginThrottleService.RecordFailure(pendingUser.Username) is { } newPauseExpiryUtc)
            {
                await _auditLogger.LogAsync("admin", pendingUser.Username, "login.paused", false, details: "Account sign-in paused after repeated failed attempts.");
                return Json(new { succeeded = false, error = BuildLoginPausedMessage(newPauseExpiryUtc) });
            }

            return Json(new { succeeded = false, error = result.ErrorMessage ?? "Passkey sign-in failed." });
        }

        _loginThrottleService.RecordSuccess(pendingUser.Username);
        await _auditLogger.LogAsync("admin", pendingUser.Username, "local-user.passkey.login", true, targetType: "LocalUser", targetId: pendingUser.Id.ToString(), details: $"Passkey used: {result.Passkey?.DisplayName}");
        await CompleteLocalSignInAsync(pendingUser);

        var redirectUrl = Url.IsLocalUrl(request.ReturnUrl)
            ? request.ReturnUrl!
            : Url.Action(nameof(PostLogin)) ?? "/";
        return Json(new { succeeded = true, redirectUrl });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasskeyRegistrationOptions()
    {
        // Fully signed-in users register from their profile; users enrolling a
        // first second factor register from the pending-login setup page.
        var user = await GetCurrentConfirmedLocalUserAsync() ?? await GetPendingSecondFactorUserAsync();
        if (user is null || !_passkeyService.IsEnabled)
        {
            return Forbid();
        }

        var optionsJson = await _passkeyService.BeginRegistrationAsync(user);
        TempData[PasskeyRegistrationOptionsTempDataKey] = optionsJson;
        return Content(optionsJson, "application/json");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasskeyRegister([FromBody] PasskeyRegisterRequest request)
    {
        var confirmedUser = await GetCurrentConfirmedLocalUserAsync();
        var pendingUser = confirmedUser is null ? await GetPendingSecondFactorUserAsync() : null;
        var user = confirmedUser ?? pendingUser;
        if (user is null || !_passkeyService.IsEnabled)
        {
            return Forbid();
        }

        if (TempData[PasskeyRegistrationOptionsTempDataKey] is not string optionsJson)
        {
            return Json(new { succeeded = false, error = "The passkey setup session expired. Try again." });
        }

        var result = await _passkeyService.CompleteRegistrationAsync(user.Id, optionsJson, request.Response ?? string.Empty, request.DisplayName);
        if (!result.Succeeded)
        {
            await _auditLogger.LogAsync(GetCurrentActorType(), user.Username, "local-user.passkey.register", false, targetType: "LocalUser", targetId: user.Id.ToString(), details: result.ErrorMessage);
            return Json(new { succeeded = false, error = result.ErrorMessage ?? "Passkey registration failed." });
        }

        await _auditLogger.LogAsync(GetCurrentActorType(), user.Username, "local-user.passkey.register", true, targetType: "LocalUser", targetId: user.Id.ToString(), details: $"Passkey registered: {result.Passkey?.DisplayName}");

        if (pendingUser is not null)
        {
            // Registering the first passkey during forced enrollment counts as
            // completing the second factor for this sign-in.
            await CompleteLocalSignInAsync(pendingUser);
            var redirectUrl = Url.IsLocalUrl(request.ReturnUrl)
                ? request.ReturnUrl!
                : Url.Action(nameof(PostLogin)) ?? "/";
            return Json(new { succeeded = true, redirectUrl });
        }

        return Json(new { succeeded = true });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasskeyRemove(Guid id)
    {
        var user = await GetCurrentConfirmedLocalUserAsync();
        if (user is null)
        {
            return Forbid();
        }

        var result = await _passkeyService.RemovePasskeyAsync(user.Id, id);
        if (!result.Succeeded)
        {
            TempData["StatusMessage"] = result.ErrorMessage ?? "The passkey could not be removed.";
            return RedirectToAction(nameof(Profile));
        }

        await _auditLogger.LogAsync(GetCurrentActorType(), user.Username, "local-user.passkey.remove", true, targetType: "LocalUser", targetId: user.Id.ToString(), details: $"Passkey removed: {result.Passkey?.DisplayName}");
        TempData["StatusMessage"] = "Passkey removed.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> TotpSetup(string? returnUrl = null)
    {
        var pendingUserId = GetPendingTotpUserId();
        var localUserId = pendingUserId ?? GetCurrentLocalUserId();
        if (localUserId is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (pendingUserId.HasValue)
        {
            var pendingUser = await _localUserService.GetByIdAsync(pendingUserId.Value);
            if (pendingUser is null)
            {
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            if (HasConfirmedTotp(pendingUser))
            {
                return RedirectToAction(nameof(Totp), new { returnUrl });
            }
        }

        var result = await _localUserService.EnsureTotpSetupAsync(localUserId.Value, GetCurrentUserIdentifier());
        if (!result.Succeeded || result.User is null || result.Setup is null)
        {
            TempData["StatusMessage"] = result.ErrorMessage ?? "Unable to start authenticator app setup.";
            return RedirectToAction(nameof(Profile));
        }

        return View(new TotpSetupViewModel
        {
            Username = result.User.Username,
            SecretKey = result.Setup.SecretKey,
            ProvisioningUri = result.Setup.ProvisioningUri,
            QrCodeImageDataUri = result.Setup.QrCodeImageDataUri,
            IsConfirmed = HasConfirmedTotp(result.User),
            IsReplacingExistingSetup = HasConfirmedTotp(result.User),
            IsPendingLogin = pendingUserId.HasValue,
            StatusMessage = TempData["StatusMessage"]?.ToString()
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TotpSetup(TotpSetupViewModel model, string? returnUrl = null)
    {
        var pendingUserId = GetPendingTotpUserId();
        var localUserId = pendingUserId ?? GetCurrentLocalUserId();
        if (localUserId is null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (pendingUserId.HasValue)
        {
            var pendingUser = await _localUserService.GetByIdAsync(pendingUserId.Value);
            if (pendingUser is null)
            {
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            if (HasConfirmedTotp(pendingUser))
            {
                return RedirectToAction(nameof(Totp), new { returnUrl });
            }
        }

        var setupResult = await _localUserService.EnsureTotpSetupAsync(localUserId.Value, GetCurrentUserIdentifier());
        if (!setupResult.Succeeded || setupResult.User is null || setupResult.Setup is null)
        {
            ModelState.AddModelError(string.Empty, setupResult.ErrorMessage ?? "Unable to load authenticator app setup.");
            return View(model);
        }

        model.Username = setupResult.User.Username;
        model.SecretKey = setupResult.Setup.SecretKey;
        model.ProvisioningUri = setupResult.Setup.ProvisioningUri;
        model.QrCodeImageDataUri = setupResult.Setup.QrCodeImageDataUri;
        model.IsConfirmed = HasConfirmedTotp(setupResult.User);
        model.IsReplacingExistingSetup = HasConfirmedTotp(setupResult.User);
        model.IsPendingLogin = pendingUserId.HasValue;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var confirmResult = await _localUserService.ConfirmTotpAsync(localUserId.Value, model.Code, GetCurrentUserIdentifier());
        if (!confirmResult.Succeeded || confirmResult.User is null)
        {
            await _auditLogger.LogAsync(GetCurrentActorType(), model.Username, "local-user.totp.confirm", false, targetType: "LocalUser", targetId: localUserId.Value.ToString(), details: confirmResult.AuditDetails);
            ModelState.AddModelError(string.Empty, confirmResult.ErrorMessage ?? "Invalid authenticator code.");
            return View(model);
        }

        await _auditLogger.LogAsync(GetCurrentActorType(), confirmResult.User.Username, "local-user.totp.confirm", true, targetType: "LocalUser", targetId: confirmResult.User.Id.ToString());

        if (GetPendingTotpUserId().HasValue)
        {
            await CompleteLocalSignInAsync(confirmResult.User);
            return RedirectToAction(nameof(PostLogin), new { returnUrl });
        }

        TempData["StatusMessage"] = model.IsReplacingExistingSetup
            ? "Authenticator app setup changed."
            : "Authenticator app setup confirmed.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public IActionResult PostLogin(string? returnUrl = null)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        if (User.IsInRole(_adminRoleName) || User.IsInRole(_userRoleName))
        {
            return RedirectToAction("Index", "Dashboard");
        }

        if (CanAccessAuditLogs())
        {
            return RedirectToAction("Audit", "Admin");
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLogin(string? returnUrl = null)
    {
        if (!_oidcAuthOptions.Enabled)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var redirectUri = ApplicationPathHelper.BuildAppPath(Request.PathBase, Url.Action(nameof(PostLogin), new { returnUrl }) ?? "/");

        await _auditLogger.LogAsync(
            "admin",
            User.FindFirstValue("preferred_username")
                ?? User.FindFirstValue("email")
                ?? User.FindFirstValue("upn")
                ?? User.FindFirstValue("unique_name")
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name
                ?? User.FindFirstValue("oid")
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? "unknown",
            "oidc.login.attempt",
            true,
            details: $"OIDC challenge initiated. returnUrl={returnUrl ?? string.Empty}");

        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var username = User.Identity?.Name ?? "unknown";

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await _auditLogger.LogAsync("admin", username, "admin.logout", true);

        if (_oidcAuthOptions.Enabled)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = ApplicationPathHelper.BuildAppPath(Request.PathBase, Url.Action(nameof(Login)) ?? "/")
            };

            return SignOut(properties, OpenIdConnectDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var model = await BuildProfileViewModelAsync();
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var profile = await BuildProfileViewModelAsync();
        profile.CurrentPassword = model.CurrentPassword;
        profile.NewPassword = model.NewPassword;
        profile.ConfirmPassword = model.ConfirmPassword;

        if (!profile.IsLocalAccount)
        {
            ModelState.AddModelError(string.Empty, "This account is managed outside the local user store.");
            return View(profile);
        }

        if (string.IsNullOrWhiteSpace(profile.CurrentPassword))
        {
            ModelState.AddModelError(nameof(profile.CurrentPassword), "The current password is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.NewPassword))
        {
            ModelState.AddModelError(nameof(profile.NewPassword), "A new password is required.");
        }
        else
        {
            AddPasswordPolicyErrors(nameof(profile.NewPassword), profile.NewPassword);
        }

        if (!string.Equals(profile.NewPassword, profile.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(profile.ConfirmPassword), "The new password confirmation does not match.");
        }

        if (!ModelState.IsValid)
        {
            return View(profile);
        }

        var localUserId = GetCurrentLocalUserId();
        if (localUserId is null)
        {
            ModelState.AddModelError(string.Empty, "Unable to resolve the current local user account.");
            return View(profile);
        }

        var result = await _localUserService.ChangeOwnPasswordAsync(localUserId.Value, profile.CurrentPassword, profile.NewPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Password change failed.");
            return View(profile);
        }

        var actor = GetCurrentUserIdentifier();
        await _auditLogger.LogAsync(GetCurrentActorType(), actor, "local-user.change-password", true, targetType: "LocalUser", targetId: localUserId.Value.ToString());
        await _usageMetricsService.RecordAsync("local-user.change-password", GetCurrentActorType(), actor, relatedId: localUserId.Value.ToString(), details: "User changed their own password.");

        TempData["StatusMessage"] = "Password updated.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<IActionResult> RecordFailedLoginAsync(AdminLoginViewModel model)
    {
        if (_loginThrottleService.RecordFailure(model.Username) is { } pauseExpiryUtc)
        {
            await _auditLogger.LogAsync("admin", model.Username, "login.paused", false, details: "Account sign-in paused after repeated failed attempts.");
            ModelState.AddModelError(string.Empty, BuildLoginPausedMessage(pauseExpiryUtc));
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return View(model);
    }

    private string BuildLoginPausedMessage(DateTime pauseExpiryUtc)
    {
        var remainingMinutes = Math.Max(1, (int)Math.Ceiling((pauseExpiryUtc - _applicationTime.UtcNow).TotalMinutes));
        return $"Too many failed sign-in attempts. Try again in {remainingMinutes} minute{(remainingMinutes == 1 ? string.Empty : "s")}.";
    }

    private bool IsLocalLoginAllowed()
    {
        if (string.Equals(_oidcAuthOptions.LocalLoginFallback, LocalLoginFallbackModes.Never, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(_oidcAuthOptions.LocalLoginFallback, LocalLoginFallbackModes.Always, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        return remoteIp is null || IPAddress.IsLoopback(remoteIp);
    }

    private async Task SignInLocalUserAsync(LocalUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("local_user_id", user.Id.ToString()),
            new("auth_source", "local")
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        foreach (var role in user.Roles.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });
    }

    private async Task SignInPendingTotpAsync(LocalUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new("pending_totp_user_id", user.Id.ToString()),
            new("auth_source", "local-totp-pending")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });
    }

    private async Task CompleteLocalSignInAsync(LocalUser user)
    {
        await SignInLocalUserAsync(user);
        await _localUserService.RecordSuccessfulLoginAsync(user.Id);

        var isAdmin = user.Roles.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(_adminRoleName, StringComparer.OrdinalIgnoreCase);
        var actorType = isAdmin ? "admin" : "user";
        var operation = isAdmin ? "admin.login" : "user.login";
        var metricKey = isAdmin ? DbUsageMetricsService.AdminLoginKey : DbUsageMetricsService.UserLoginKey;

        await _auditLogger.LogAsync(actorType, user.Username, operation, true);
        await _usageMetricsService.RecordAsync(metricKey, actorType, user.Username, details: "Local user sign-in succeeded.");
    }

    private async Task<ProfileViewModel> BuildProfileViewModelAsync()
    {
        var localUserId = GetCurrentLocalUserId();
        LocalUser? localUser = null;

        if (localUserId.HasValue)
        {
            localUser = await _localUserService.GetByIdAsync(localUserId.Value);
        }

        var roles = User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var passkeys = localUser is not null && _passkeyService.IsEnabled
            ? await _passkeyService.GetPasskeysAsync(localUser.Id)
            : Array.Empty<LocalUserPasskey>();

        return new ProfileViewModel
        {
            Username = localUser?.Username ?? GetCurrentUserIdentifier(),
            DisplayName = localUser?.DisplayName ?? GetCurrentUserIdentifier(),
            Email = localUser?.Email ?? User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Roles = roles,
            IsLocalAccount = localUser is not null,
            IsTotpRequired = localUser?.IsTotpRequired ?? false,
            IsTotpConfigured = localUser is not null && HasConfirmedTotp(localUser),
            IsPasskeySupportEnabled = _passkeyService.IsEnabled,
            Passkeys = passkeys
                .Select(x => new PasskeyListItemViewModel
                {
                    Id = x.Id,
                    DisplayName = x.DisplayName,
                    CreatedAtUtc = x.CreatedAtUtc,
                    LastUsedAtUtc = x.LastUsedAtUtc
                })
                .ToList(),
            LastLoginAtUtc = localUser?.LastLoginAtUtc,
            LastShareCreatedAtUtc = localUser?.LastShareCreatedAtUtc,
            LastPasswordResetAtUtc = localUser?.LastPasswordResetAtUtc,
            TotalSuccessfulLogins = localUser?.TotalSuccessfulLogins ?? 0,
            TotalSharesCreated = localUser?.TotalSharesCreated ?? 0,
            StatusMessage = TempData["StatusMessage"]?.ToString()
        };
    }

    private Guid? GetCurrentLocalUserId()
    {
        var raw = User.FindFirstValue("local_user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var localUserId) ? localUserId : null;
    }

    private Guid? GetPendingTotpUserId()
    {
        var raw = User.FindFirstValue("pending_totp_user_id");
        return Guid.TryParse(raw, out var localUserId) ? localUserId : null;
    }

    private async Task<LocalUser?> GetPendingSecondFactorUserAsync()
    {
        var pendingUserId = GetPendingTotpUserId();
        if (pendingUserId is null)
        {
            return null;
        }

        var user = await _localUserService.GetByIdAsync(pendingUserId.Value);
        return user is null || user.IsDisabled ? null : user;
    }

    private async Task<LocalUser?> GetCurrentConfirmedLocalUserAsync()
    {
        if (GetPendingTotpUserId() is not null)
        {
            return null;
        }

        var localUserId = GetCurrentLocalUserId();
        if (localUserId is null)
        {
            return null;
        }

        var user = await _localUserService.GetByIdAsync(localUserId.Value);
        return user is null || user.IsDisabled ? null : user;
    }

    private static bool RequiresTotp(LocalUser user)
    {
        return user.IsTotpRequired || HasConfirmedTotp(user);
    }

    private static bool HasConfirmedTotp(LocalUser user)
    {
        return !string.IsNullOrWhiteSpace(user.TotpSecretEncrypted) && user.TotpConfirmedAtUtc is not null;
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

    private bool CanAccessAuditLogs()
    {
        return User.IsInRole(_adminRoleName) || User.IsInRole(BuiltInRoleNames.Auditor);
    }

    private void AddPasswordPolicyErrors(string key, string password)
    {
        foreach (var error in LocalUserPasswordPolicy.Validate(password))
        {
            ModelState.AddModelError(key, error);
        }
    }

}
