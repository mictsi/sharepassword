using Sekura.Models;

namespace Sekura.Services;

public interface IShareStore
{
    Task<IReadOnlyCollection<SecureShare>> GetAllSharesAsync(CancellationToken cancellationToken = default);
    Task<SecureShare?> GetShareByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SecureShare?> GetShareByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task UpsertShareAsync(SecureShare share, CancellationToken cancellationToken = default);
    Task DeleteShareAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredSharesAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
