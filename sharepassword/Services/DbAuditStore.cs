using Microsoft.EntityFrameworkCore;
using SharePassword.Data;
using SharePassword.Models;

namespace SharePassword.Services;

public class DbAuditStore : IAuditLogReader, IAuditLogSink
{
    private readonly ISharePasswordDbContextFactory _dbContextFactory;

    public DbAuditStore(ISharePasswordDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = CloneAudit(auditLog);

        dbContext.AuditLogs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        auditLog.Id = entity.Id;
        auditLog.TimestampUtc = entity.TimestampUtc;
    }

    public async Task<IReadOnlyCollection<AuditLog>> GetLatestAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return Array.Empty<AuditLog>();
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static AuditLog CloneAudit(AuditLog auditLog)
    {
        return new AuditLog
        {
            TimestampUtc = EnsureUtc(auditLog.TimestampUtc == default ? DateTime.UtcNow : auditLog.TimestampUtc),
            ActorType = auditLog.ActorType ?? string.Empty,
            ActorIdentifier = auditLog.ActorIdentifier ?? string.Empty,
            Operation = auditLog.Operation ?? string.Empty,
            Success = auditLog.Success,
            TargetType = auditLog.TargetType,
            TargetId = auditLog.TargetId,
            IpAddress = auditLog.IpAddress,
            UserAgent = auditLog.UserAgent,
            CorrelationId = auditLog.CorrelationId,
            Details = auditLog.Details
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}