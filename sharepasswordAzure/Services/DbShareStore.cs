using Microsoft.EntityFrameworkCore;
using SharePassword.Data;
using SharePassword.Models;

namespace SharePassword.Services;

public class DbShareStore : IShareStore
{
    private readonly ISharePasswordDbContextFactory _dbContextFactory;

    public DbShareStore(ISharePasswordDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyCollection<PasswordShare>> GetAllSharesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.PasswordShares
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PasswordShare?> GetShareByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.PasswordShares
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PasswordShare?> GetShareByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var normalizedToken = NormalizeToken(token);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.PasswordShares
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AccessToken == normalizedToken, cancellationToken);
    }

    public async Task UpsertShareAsync(PasswordShare share, CancellationToken cancellationToken = default)
    {
        var normalizedShare = CloneShare(share);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.PasswordShares
            .SingleOrDefaultAsync(x => x.Id == normalizedShare.Id, cancellationToken);

        if (existing is null)
        {
            dbContext.PasswordShares.Add(normalizedShare);
        }
        else
        {
            CopyShare(normalizedShare, existing);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteShareAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.PasswordShares
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredSharesAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var normalizedUtcNow = EnsureUtc(utcNow);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.PasswordShares
            .Where(x => x.ExpiresAtUtc <= normalizedUtcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static PasswordShare CloneShare(PasswordShare share)
    {
        return new PasswordShare
        {
            Id = share.Id,
            RecipientEmail = (share.RecipientEmail ?? string.Empty).Trim().ToLowerInvariant(),
            SharedUsername = share.SharedUsername ?? string.Empty,
            EncryptedPassword = share.EncryptedPassword ?? string.Empty,
            Instructions = share.Instructions ?? string.Empty,
            AccessCodeHash = share.AccessCodeHash ?? string.Empty,
            AccessToken = NormalizeToken(share.AccessToken),
            CreatedAtUtc = EnsureUtc(share.CreatedAtUtc),
            ExpiresAtUtc = EnsureUtc(share.ExpiresAtUtc),
            LastAccessedAtUtc = EnsureUtc(share.LastAccessedAtUtc),
            CreatedBy = share.CreatedBy ?? string.Empty,
            RequireOidcLogin = share.RequireOidcLogin
        };
    }

    private static void CopyShare(PasswordShare source, PasswordShare target)
    {
        target.RecipientEmail = source.RecipientEmail;
        target.SharedUsername = source.SharedUsername;
        target.EncryptedPassword = source.EncryptedPassword;
        target.Instructions = source.Instructions;
        target.AccessCodeHash = source.AccessCodeHash;
        target.AccessToken = source.AccessToken;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.ExpiresAtUtc = source.ExpiresAtUtc;
        target.LastAccessedAtUtc = source.LastAccessedAtUtc;
        target.CreatedBy = source.CreatedBy;
        target.RequireOidcLogin = source.RequireOidcLogin;
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