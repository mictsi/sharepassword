using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;
using SharePassword.Models;
using SharePassword.Options;
using System.Text.Json;

namespace SharePassword.Services;

public class KeyVaultStore : IShareStore, IInformationRequestStore
{
    private const string ShareKind = "share";
    private const string InformationRequestKind = "information-request";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SecretClient _secretClient;
    private readonly string _prefix;

    public KeyVaultStore(SecretClient secretClient, IOptions<AzureKeyVaultOptions> options)
    {
        _secretClient = secretClient;
        _prefix = string.IsNullOrWhiteSpace(options.Value.SecretPrefix) ? "sharepassword" : options.Value.SecretPrefix.Trim().ToLowerInvariant();
    }

    public async Task<IReadOnlyCollection<PasswordShare>> GetAllSharesAsync(CancellationToken cancellationToken = default)
    {
        var shares = new List<PasswordShare>();

        await foreach (var properties in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            if (properties.Enabled == false || !properties.Name.StartsWith(GetSharePrefix(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var share = await TryGetShareFromSecretNameAsync(properties.Name, cancellationToken);
            if (share is not null)
            {
                shares.Add(share);
            }
        }

        return shares;
    }

    public async Task<PasswordShare?> GetShareByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await TryGetShareFromSecretNameAsync(GetShareSecretName(id), cancellationToken);
    }

    public async Task<PasswordShare?> GetShareByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        await foreach (var properties in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            if (properties.Enabled == false || !properties.Name.StartsWith(GetSharePrefix(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!properties.Tags.TryGetValue("token", out var tokenTag) || !string.Equals(tokenTag, token, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return await TryGetShareFromSecretNameAsync(properties.Name, cancellationToken);
        }

        return null;
    }

    public async Task UpsertShareAsync(PasswordShare share, CancellationToken cancellationToken = default)
    {
        var secret = new KeyVaultSecret(GetShareSecretName(share.Id), JsonSerializer.Serialize(share, JsonOptions));
        secret.Properties.ContentType = "application/json";
        secret.Properties.Tags["kind"] = ShareKind;
        secret.Properties.Tags["id"] = share.Id.ToString("N");
        secret.Properties.Tags["token"] = share.AccessToken;
        secret.Properties.Tags["expiresUtc"] = share.ExpiresAtUtc.ToString("O");
        secret.Properties.Tags["recipient"] = share.RecipientEmail;

        await _secretClient.SetSecretAsync(secret, cancellationToken);
    }

    public async Task DeleteShareAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await DeleteSecretIfExistsAsync(GetShareSecretName(id), cancellationToken);
    }

    public async Task<int> DeleteExpiredSharesAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var expiredIds = await GetExpiredIdsAsync(GetSharePrefix(), utcNow, cancellationToken);

        foreach (var id in expiredIds)
        {
            await DeleteShareAsync(id, cancellationToken);
        }

        return expiredIds.Count;
    }

    public async Task<IReadOnlyCollection<InformationRequest>> GetAllInformationRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = new List<InformationRequest>();

        await foreach (var properties in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            if (properties.Enabled == false || !properties.Name.StartsWith(GetInformationRequestPrefix(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var request = await TryGetInformationRequestFromSecretNameAsync(properties.Name, cancellationToken);
            if (request is not null)
            {
                requests.Add(request);
            }
        }

        return requests;
    }

    public async Task<InformationRequest?> GetInformationRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await TryGetInformationRequestFromSecretNameAsync(GetInformationRequestSecretName(id), cancellationToken);
    }

    public async Task<InformationRequest?> GetInformationRequestByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        await foreach (var properties in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            if (properties.Enabled == false || !properties.Name.StartsWith(GetInformationRequestPrefix(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!properties.Tags.TryGetValue("token", out var tokenTag) || !string.Equals(tokenTag, token, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return await TryGetInformationRequestFromSecretNameAsync(properties.Name, cancellationToken);
        }

        return null;
    }

    public async Task UpsertInformationRequestAsync(InformationRequest request, CancellationToken cancellationToken = default)
    {
        var secret = new KeyVaultSecret(GetInformationRequestSecretName(request.Id), JsonSerializer.Serialize(request, JsonOptions));
        secret.Properties.ContentType = "application/json";
        secret.Properties.Tags["kind"] = InformationRequestKind;
        secret.Properties.Tags["id"] = request.Id.ToString("N");
        secret.Properties.Tags["token"] = request.AccessToken;
        secret.Properties.Tags["expiresUtc"] = request.ExpiresAtUtc.ToString("O");
        secret.Properties.Tags["partner"] = request.PartnerEmail;

        await _secretClient.SetSecretAsync(secret, cancellationToken);
    }

    public async Task DeleteInformationRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await DeleteSecretIfExistsAsync(GetInformationRequestSecretName(id), cancellationToken);
    }

    public async Task<int> DeleteExpiredInformationRequestsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var expiredIds = await GetExpiredIdsAsync(GetInformationRequestPrefix(), utcNow, cancellationToken);

        foreach (var id in expiredIds)
        {
            await DeleteInformationRequestAsync(id, cancellationToken);
        }

        return expiredIds.Count;
    }

    private async Task<List<Guid>> GetExpiredIdsAsync(string secretPrefix, DateTime utcNow, CancellationToken cancellationToken)
    {
        var expiredIds = new List<Guid>();

        await foreach (var properties in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            if (properties.Enabled == false || !properties.Name.StartsWith(secretPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!properties.Tags.TryGetValue("expiresUtc", out var expiresRaw) || !DateTime.TryParse(expiresRaw, out var expiresUtc))
            {
                continue;
            }

            if (expiresUtc <= utcNow && properties.Tags.TryGetValue("id", out var idRaw) && Guid.TryParse(idRaw, out var id))
            {
                expiredIds.Add(id);
            }
        }

        return expiredIds;
    }

    private async Task<PasswordShare?> TryGetShareFromSecretNameAsync(string secretName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return JsonSerializer.Deserialize<PasswordShare>(response.Value.Value, JsonOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task<InformationRequest?> TryGetInformationRequestFromSecretNameAsync(string secretName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return JsonSerializer.Deserialize<InformationRequest>(response.Value.Value, JsonOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task DeleteSecretIfExistsAsync(string secretName, CancellationToken cancellationToken)
    {
        try
        {
            await _secretClient.StartDeleteSecretAsync(secretName, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
        }
    }

    private string GetSharePrefix() => $"{_prefix}-share-";

    private string GetInformationRequestPrefix() => $"{_prefix}-request-";

    private string GetShareSecretName(Guid id) => $"{GetSharePrefix()}{id:N}";

    private string GetInformationRequestSecretName(Guid id) => $"{GetInformationRequestPrefix()}{id:N}";
}
