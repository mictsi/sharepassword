using SharePassword.Models;

namespace SharePassword.Services;

public interface IInformationRequestStore
{
    Task<IReadOnlyCollection<InformationRequest>> GetAllInformationRequestsAsync(CancellationToken cancellationToken = default);
    Task<InformationRequest?> GetInformationRequestByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InformationRequest?> GetInformationRequestByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task UpsertInformationRequestAsync(InformationRequest request, CancellationToken cancellationToken = default);
    Task DeleteInformationRequestAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredInformationRequestsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
