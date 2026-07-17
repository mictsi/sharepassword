using Microsoft.EntityFrameworkCore;
using Sekura.Data;
using Sekura.Models;

namespace Sekura.Services;

public class DbShareStore : IShareStore
{
    private readonly ISekuraDbContextFactory _dbContextFactory;
    private readonly IDatabaseOperationRunner _databaseOperationRunner;

    public DbShareStore(ISekuraDbContextFactory dbContextFactory, IDatabaseOperationRunner databaseOperationRunner)
    {
        _dbContextFactory = dbContextFactory;
        _databaseOperationRunner = databaseOperationRunner;
    }

    public async Task<IReadOnlyCollection<SecureShare>> GetAllSharesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteReadAsync(
            "load secure shares",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                return await dbContext.SecureShares
                    .AsNoTracking()
                    .ToListAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task<SecureShare?> GetShareByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ExecuteReadAsync(
            "load secure share by id",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                return await dbContext.SecureShares
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == id, innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task<SecureShare?> GetShareByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var normalizedToken = NormalizeToken(token);
        return await ExecuteReadAsync(
            "load secure share by token",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                return await dbContext.SecureShares
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.AccessToken == normalizedToken, innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task UpsertShareAsync(SecureShare share, CancellationToken cancellationToken = default)
    {
        var normalizedShare = CloneShare(share);
        await ExecuteWriteAsync(
            "upsert secure share",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                var existing = await dbContext.SecureShares
                    .SingleOrDefaultAsync(x => x.Id == normalizedShare.Id, innerCancellationToken);

                if (existing is null)
                {
                    dbContext.SecureShares.Add(normalizedShare);
                }
                else
                {
                    CopyShare(normalizedShare, existing);
                }

                await dbContext.SaveChangesAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task DeleteShareAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExecuteWriteAsync(
            "delete secure share",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                await dbContext.SecureShares
                    .Where(x => x.Id == id)
                    .ExecuteDeleteAsync(innerCancellationToken);
            },
            cancellationToken);
    }

    public async Task<int> DeleteExpiredSharesAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var normalizedUtcNow = EnsureUtc(utcNow);
        return await ExecuteWriteAsync(
            "delete expired secure shares",
            async innerCancellationToken =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(innerCancellationToken);

                return await dbContext.SecureShares
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

    private static SecureShare CloneShare(SecureShare share)
    {
        return new SecureShare
        {
            Id = share.Id,
            RecipientEmail = (share.RecipientEmail ?? string.Empty).Trim().ToLowerInvariant(),
            SharedUsername = share.SharedUsername ?? string.Empty,
            EncryptedPassword = share.EncryptedPassword ?? string.Empty,
            SecretEncryptionMode = SecretEncryptionModes.Normalize(share.SecretEncryptionMode),
            Instructions = share.Instructions ?? string.Empty,
            AccessCodeHash = share.AccessCodeHash ?? string.Empty,
            AccessToken = NormalizeToken(share.AccessToken),
            CreatedAtUtc = EnsureUtc(share.CreatedAtUtc),
            ExpiresAtUtc = EnsureUtc(share.ExpiresAtUtc),
            LastAccessedAtUtc = EnsureUtc(share.LastAccessedAtUtc),
            CreatedBy = share.CreatedBy ?? string.Empty,
            RequireOidcLogin = share.RequireOidcLogin,
            FailedAccessAttempts = Math.Max(0, share.FailedAccessAttempts),
            AccessPausedUntilUtc = EnsureUtc(share.AccessPausedUntilUtc)
        };
    }

    private static void CopyShare(SecureShare source, SecureShare target)
    {
        target.RecipientEmail = source.RecipientEmail;
        target.SharedUsername = source.SharedUsername;
        target.EncryptedPassword = source.EncryptedPassword;
        target.SecretEncryptionMode = source.SecretEncryptionMode;
        target.Instructions = source.Instructions;
        target.AccessCodeHash = source.AccessCodeHash;
        target.AccessToken = source.AccessToken;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.ExpiresAtUtc = source.ExpiresAtUtc;
        target.LastAccessedAtUtc = source.LastAccessedAtUtc;
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
