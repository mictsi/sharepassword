using Microsoft.EntityFrameworkCore;
using SharePassword.Data;
using SharePassword.Models;

namespace SharePassword.Services;

public class DbInformationRequestStore : IInformationRequestStore
{
    private readonly ISharePasswordDbContextFactory _dbContextFactory;
    private readonly IDatabaseOperationRunner _databaseOperationRunner;

    public DbInformationRequestStore(ISharePasswordDbContextFactory dbContextFactory, IDatabaseOperationRunner databaseOperationRunner)
    {
        _dbContextFactory = dbContextFactory;
        _databaseOperationRunner = databaseOperationRunner;
    }

    public async Task<IReadOnlyCollection<InformationRequest>> GetAllInformationRequestsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteReadAsync(
            "load information requests",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                return await dbContext.InformationRequests
                    .AsNoTracking()
                    .ToListAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task<InformationRequest?> GetInformationRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ExecuteReadAsync(
            "load information request by id",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                return await dbContext.InformationRequests
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == id, innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task<InformationRequest?> GetInformationRequestByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var normalizedToken = NormalizeToken(token);
        return await ExecuteReadAsync(
            "load information request by token",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                return await dbContext.InformationRequests
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.AccessToken == normalizedToken, innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task UpsertInformationRequestAsync(InformationRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRequest = CloneRequest(request);
        await ExecuteWriteAsync(
            "upsert information request",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                var existing = await dbContext.InformationRequests
                    .SingleOrDefaultAsync(x => x.Id == normalizedRequest.Id, innerCancellationToken);

                if (existing is null)
                {
                    dbContext.InformationRequests.Add(normalizedRequest);
                }
                else
                {
                    CopyRequest(normalizedRequest, existing);
                }

                await dbContext.SaveChangesAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task DeleteInformationRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExecuteWriteAsync(
            "delete information request",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                await dbContext.InformationRequests
                    .Where(x => x.Id == id)
                    .ExecuteDeleteAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task<int> DeleteExpiredInformationRequestsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var normalizedUtcNow = EnsureUtc(utcNow);
        return await ExecuteWriteAsync(
            "delete expired information requests",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                return await dbContext.InformationRequests
                    .Where(x => x.ExpiresAtUtc <= normalizedUtcNow)
                    .ExecuteDeleteAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    private Task<T> ExecuteReadAsync<T>(string operationName, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        return _databaseOperationRunner.ExecuteAsync(operationName, DatabaseOperationPurpose.Read, operation, cancellationToken);
    }

    private Task ExecuteWriteAsync(string operationName, Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        return _databaseOperationRunner.ExecuteAsync(operationName, DatabaseOperationPurpose.Write, operation, cancellationToken);
    }

    private Task<T> ExecuteWriteAsync<T>(string operationName, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        return _databaseOperationRunner.ExecuteAsync(operationName, DatabaseOperationPurpose.Write, operation, cancellationToken);
    }

    private static InformationRequest CloneRequest(InformationRequest request)
    {
        return new InformationRequest
        {
            Id = request.Id,
            PartnerEmail = (request.PartnerEmail ?? string.Empty).Trim().ToLowerInvariant(),
            RequestInstructions = request.RequestInstructions ?? string.Empty,
            EncryptedPartnerResponse = request.EncryptedPartnerResponse ?? string.Empty,
            ResponseEncryptionMode = SecretEncryptionModes.Normalize(request.ResponseEncryptionMode),
            AccessCodeHash = request.AccessCodeHash ?? string.Empty,
            AccessToken = NormalizeToken(request.AccessToken),
            CreatedAtUtc = EnsureUtc(request.CreatedAtUtc),
            ExpiresAtUtc = EnsureUtc(request.ExpiresAtUtc),
            LastSubmittedAtUtc = EnsureUtc(request.LastSubmittedAtUtc),
            CreatedBy = request.CreatedBy ?? string.Empty,
            RequireOidcLogin = request.RequireOidcLogin,
            FailedAccessAttempts = Math.Max(0, request.FailedAccessAttempts),
            AccessPausedUntilUtc = EnsureUtc(request.AccessPausedUntilUtc)
        };
    }

    private static void CopyRequest(InformationRequest source, InformationRequest target)
    {
        target.PartnerEmail = source.PartnerEmail;
        target.RequestInstructions = source.RequestInstructions;
        target.EncryptedPartnerResponse = source.EncryptedPartnerResponse;
        target.ResponseEncryptionMode = source.ResponseEncryptionMode;
        target.AccessCodeHash = source.AccessCodeHash;
        target.AccessToken = source.AccessToken;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.ExpiresAtUtc = source.ExpiresAtUtc;
        target.LastSubmittedAtUtc = source.LastSubmittedAtUtc;
        target.CreatedBy = source.CreatedBy;
        target.RequireOidcLogin = source.RequireOidcLogin;
        target.FailedAccessAttempts = source.FailedAccessAttempts;
        target.AccessPausedUntilUtc = source.AccessPausedUntilUtc;
    }

    private static string NormalizeToken(string? token)
    {
        return (token ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value == default)
        {
            return value;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? EnsureUtc(DateTime? value)
    {
        return value is null ? null : EnsureUtc(value.Value);
    }
}
