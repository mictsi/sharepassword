using Sekura.Models;

namespace Sekura.Services;

public interface IAuditLogReader
{
    Task<IReadOnlyCollection<AuditLog>> GetLatestAsync(int take, CancellationToken cancellationToken = default);
}
