using System.Buffers.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using SharePassword.Data;
using SharePassword.Models;

namespace SharePassword.Services;

public interface IPasskeyService
{
    bool IsEnabled { get; }

    Task<IReadOnlyCollection<LocalUserPasskey>> GetPasskeysAsync(Guid localUserId, CancellationToken cancellationToken = default);
    Task<bool> HasPasskeysAsync(Guid localUserId, CancellationToken cancellationToken = default);

    /// <summary>Creates registration (attestation) options for the user. The returned JSON must be passed back to <see cref="CompleteRegistrationAsync"/>.</summary>
    Task<string> BeginRegistrationAsync(LocalUser user, CancellationToken cancellationToken = default);
    Task<PasskeyOperationResult> CompleteRegistrationAsync(Guid localUserId, string optionsJson, string attestationResponseJson, string? displayName, CancellationToken cancellationToken = default);

    /// <summary>Creates assertion options limited to the user's registered passkeys, or null when none exist.</summary>
    Task<string?> BeginAssertionAsync(Guid localUserId, CancellationToken cancellationToken = default);
    Task<PasskeyOperationResult> CompleteAssertionAsync(Guid localUserId, string optionsJson, string assertionResponseJson, CancellationToken cancellationToken = default);

    Task<PasskeyOperationResult> RemovePasskeyAsync(Guid localUserId, Guid passkeyId, CancellationToken cancellationToken = default);
    Task<int> RemoveAllPasskeysAsync(Guid localUserId, CancellationToken cancellationToken = default);
}

public sealed record PasskeyOperationResult(bool Succeeded, string? ErrorMessage = null, LocalUserPasskey? Passkey = null)
{
    public static PasskeyOperationResult Success(LocalUserPasskey? passkey = null) => new(true, Passkey: passkey);
    public static PasskeyOperationResult Failed(string errorMessage) => new(false, errorMessage);
}

public sealed class PasskeyService : IPasskeyService
{
    private const int MaxPasskeysPerUser = 10;

    private readonly IFido2 _fido2;
    private readonly ISharePasswordDbContextFactory _dbContextFactory;
    private readonly IDatabaseOperationRunner _databaseOperationRunner;
    private readonly bool _enabled;

    public PasskeyService(
        IFido2 fido2,
        ISharePasswordDbContextFactory dbContextFactory,
        IDatabaseOperationRunner databaseOperationRunner,
        Microsoft.Extensions.Options.IOptions<Options.PasskeyOptions> passkeyOptions)
    {
        _fido2 = fido2;
        _dbContextFactory = dbContextFactory;
        _databaseOperationRunner = databaseOperationRunner;
        _enabled = passkeyOptions.Value.Enabled;
    }

    public bool IsEnabled => _enabled;

    public async Task<IReadOnlyCollection<LocalUserPasskey>> GetPasskeysAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        return await _databaseOperationRunner.ExecuteAsync(
            "load local user passkeys",
            DatabaseOperationPurpose.Read,
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);
                return (IReadOnlyCollection<LocalUserPasskey>)await dbContext.LocalUserPasskeys
                    .AsNoTracking()
                    .Where(x => x.LocalUserId == localUserId)
                    .OrderBy(x => x.CreatedAtUtc)
                    .ToListAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task<bool> HasPasskeysAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        return await _databaseOperationRunner.ExecuteAsync(
            "check local user passkeys",
            DatabaseOperationPurpose.Read,
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);
                return await dbContext.LocalUserPasskeys.AnyAsync(x => x.LocalUserId == localUserId, innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task<string> BeginRegistrationAsync(LocalUser user, CancellationToken cancellationToken = default)
    {
        var existing = await GetPasskeysAsync(user.Id, cancellationToken);
        var excludeCredentials = existing
            .Select(x => new PublicKeyCredentialDescriptor(Base64Url.DecodeFromChars(x.CredentialId)))
            .ToList();

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = user.Id.ToByteArray(),
                Name = user.Username,
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName
            },
            ExcludeCredentials = excludeCredentials,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Preferred,
                UserVerification = UserVerificationRequirement.Preferred
            },
            AttestationPreference = AttestationConveyancePreference.None
        });

        return options.ToJson();
    }

    public async Task<PasskeyOperationResult> CompleteRegistrationAsync(Guid localUserId, string optionsJson, string attestationResponseJson, string? displayName, CancellationToken cancellationToken = default)
    {
        CredentialCreateOptions options;
        AuthenticatorAttestationRawResponse? attestationResponse;
        try
        {
            options = CredentialCreateOptions.FromJson(optionsJson);
            attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationResponseJson);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException)
        {
            return PasskeyOperationResult.Failed("The passkey registration response could not be read.");
        }

        if (attestationResponse is null)
        {
            return PasskeyOperationResult.Failed("The passkey registration response could not be read.");
        }

        if (!new Guid(options.User.Id).Equals(localUserId))
        {
            return PasskeyOperationResult.Failed("The passkey registration request does not belong to the signed-in user.");
        }

        RegisteredPublicKeyCredential credential;
        try
        {
            credential = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (parameters, innerCancellationToken) =>
                {
                    var candidateId = Base64Url.EncodeToString(parameters.CredentialId);
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);
                    return !await dbContext.LocalUserPasskeys.AnyAsync(x => x.CredentialId == candidateId, innerCancellationToken);
                }
            }, cancellationToken);
        }
        catch (Fido2VerificationException exception)
        {
            return PasskeyOperationResult.Failed($"Passkey registration failed: {exception.Message}");
        }

        var normalizedDisplayName = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalizedDisplayName))
        {
            normalizedDisplayName = "Passkey";
        }
        else if (normalizedDisplayName.Length > 128)
        {
            normalizedDisplayName = normalizedDisplayName[..128];
        }

        var passkey = new LocalUserPasskey
        {
            Id = Guid.NewGuid(),
            LocalUserId = localUserId,
            CredentialId = Base64Url.EncodeToString(credential.Id),
            PublicKey = Convert.ToBase64String(credential.PublicKey),
            SignatureCounter = credential.SignCount,
            Transports = string.Join(';', credential.Transports?.Select(transport => transport.ToString()) ?? []),
            AaGuid = credential.AaGuid,
            DisplayName = normalizedDisplayName,
            CreatedAtUtc = DateTime.UtcNow
        };

        return await _databaseOperationRunner.ExecuteAsync(
            "register local user passkey",
            DatabaseOperationPurpose.Write,
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);
                var passkeyCount = await dbContext.LocalUserPasskeys.CountAsync(x => x.LocalUserId == localUserId, innerCancellationToken);
                if (passkeyCount >= MaxPasskeysPerUser)
                {
                    return PasskeyOperationResult.Failed($"A maximum of {MaxPasskeysPerUser} passkeys can be registered per account.");
                }

                dbContext.LocalUserPasskeys.Add(passkey);
                await dbContext.SaveChangesAsync(innerCancellationToken);
                return PasskeyOperationResult.Success(passkey);
            },
            cancellationToken);
    }

    public async Task<string?> BeginAssertionAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        var passkeys = await GetPasskeysAsync(localUserId, cancellationToken);
        if (passkeys.Count == 0)
        {
            return null;
        }

        var allowedCredentials = passkeys
            .Select(x => new PublicKeyCredentialDescriptor(Base64Url.DecodeFromChars(x.CredentialId)))
            .ToList();

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Preferred
        });

        return options.ToJson();
    }

    public async Task<PasskeyOperationResult> CompleteAssertionAsync(Guid localUserId, string optionsJson, string assertionResponseJson, CancellationToken cancellationToken = default)
    {
        AssertionOptions options;
        AuthenticatorAssertionRawResponse? assertionResponse;
        try
        {
            options = AssertionOptions.FromJson(optionsJson);
            assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionResponseJson);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException)
        {
            return PasskeyOperationResult.Failed("The passkey sign-in response could not be read.");
        }

        if (assertionResponse is null)
        {
            return PasskeyOperationResult.Failed("The passkey sign-in response could not be read.");
        }

        var credentialId = Base64Url.EncodeToString(assertionResponse.RawId);
        var passkey = await _databaseOperationRunner.ExecuteAsync(
            "load local user passkey by credential",
            DatabaseOperationPurpose.Read,
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);
                return await dbContext.LocalUserPasskeys
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.CredentialId == credentialId, innerCancellationToken);
            },
            cancellationToken);

        if (passkey is null || passkey.LocalUserId != localUserId)
        {
            return PasskeyOperationResult.Failed("This passkey is not registered for the account.");
        }

        VerifyAssertionResult verifyResult;
        try
        {
            verifyResult = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = options,
                StoredPublicKey = Convert.FromBase64String(passkey.PublicKey),
                StoredSignatureCounter = (uint)Math.Max(0, passkey.SignatureCounter),
                IsUserHandleOwnerOfCredentialIdCallback = (parameters, _) =>
                    Task.FromResult(new Guid(parameters.UserHandle).Equals(localUserId))
            }, cancellationToken);
        }
        catch (Fido2VerificationException exception)
        {
            return PasskeyOperationResult.Failed($"Passkey verification failed: {exception.Message}");
        }

        await _databaseOperationRunner.ExecuteAsync(
            "record local user passkey sign-in",
            DatabaseOperationPurpose.Write,
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);
                var storedPasskey = await dbContext.LocalUserPasskeys
                    .SingleOrDefaultAsync(x => x.Id == passkey.Id, innerCancellationToken);
                if (storedPasskey is null)
                {
                    return;
                }

                storedPasskey.SignatureCounter = verifyResult.SignCount;
                storedPasskey.LastUsedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(innerCancellationToken);
            },
            cancellationToken);

        return PasskeyOperationResult.Success(passkey);
    }

    public async Task<PasskeyOperationResult> RemovePasskeyAsync(Guid localUserId, Guid passkeyId, CancellationToken cancellationToken = default)
    {
        return await _databaseOperationRunner.ExecuteAsync(
            "remove local user passkey",
            DatabaseOperationPurpose.Write,
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);
                var passkey = await dbContext.LocalUserPasskeys
                    .SingleOrDefaultAsync(x => x.Id == passkeyId && x.LocalUserId == localUserId, innerCancellationToken);
                if (passkey is null)
                {
                    return PasskeyOperationResult.Failed("The selected passkey could not be found.");
                }

                dbContext.LocalUserPasskeys.Remove(passkey);
                await dbContext.SaveChangesAsync(innerCancellationToken);
                return PasskeyOperationResult.Success(passkey);
            },
            cancellationToken);
    }

    public async Task<int> RemoveAllPasskeysAsync(Guid localUserId, CancellationToken cancellationToken = default)
    {
        return await _databaseOperationRunner.ExecuteAsync(
            "remove all local user passkeys",
            DatabaseOperationPurpose.Write,
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);
                var passkeys = await dbContext.LocalUserPasskeys
                    .Where(x => x.LocalUserId == localUserId)
                    .ToListAsync(innerCancellationToken);
                if (passkeys.Count == 0)
                {
                    return 0;
                }

                dbContext.LocalUserPasskeys.RemoveRange(passkeys);
                await dbContext.SaveChangesAsync(innerCancellationToken);
                return passkeys.Count;
            },
            cancellationToken);
    }
}
