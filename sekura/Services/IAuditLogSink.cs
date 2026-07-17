using Sekura.Models;

namespace Sekura.Services;

public interface IAuditLogSink
{
    Task AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}
