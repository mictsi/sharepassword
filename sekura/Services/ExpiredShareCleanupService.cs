using Microsoft.Extensions.Options;
using Sekura.Options;

namespace Sekura.Services;

public class ExpiredShareCleanupService : BackgroundService
{
    private readonly IShareStore _shareStore;
    private readonly IInformationRequestStore _informationRequestStore;
    private readonly IAuditLogger _auditLogger;
    private readonly IApplicationTime _applicationTime;
    private readonly IUsageMetricsService _usageMetricsService;
    private readonly ILogger<ExpiredShareCleanupService> _logger;
    private readonly ShareOptions _shareOptions;

    public ExpiredShareCleanupService(
        IShareStore shareStore,
        IInformationRequestStore informationRequestStore,
        IAuditLogger auditLogger,
        IApplicationTime applicationTime,
        IUsageMetricsService usageMetricsService,
        IOptions<ShareOptions> options,
        ILogger<ExpiredShareCleanupService> logger)
    {
        _shareStore = shareStore;
        _informationRequestStore = informationRequestStore;
        _auditLogger = auditLogger;
        _applicationTime = applicationTime;
        _usageMetricsService = usageMetricsService;
        _logger = logger;
        _shareOptions = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = Math.Max(15, _shareOptions.CleanupIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = _applicationTime.UtcNow;
                await DeleteExpiredSharesAsync(nowUtc, stoppingToken);
                await DeleteExpiredInformationRequestsAsync(nowUtc, stoppingToken);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while cleaning expired shares and information requests.");
            }

            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }

    private async Task DeleteExpiredSharesAsync(DateTime nowUtc, CancellationToken stoppingToken)
    {
        var expiredShares = await _shareStore.GetAllSharesAsync(stoppingToken);
        var expiredUnusedCount = expiredShares.Count(x => x.ExpiresAtUtc <= nowUtc && !x.LastAccessedAtUtc.HasValue);
        var deletedCount = await _shareStore.DeleteExpiredSharesAsync(nowUtc, stoppingToken);

        if (deletedCount <= 0)
        {
            return;
        }

        await _auditLogger.LogAsync(
            "system",
            "cleanup-service",
            "cleanup.expired-shares",
            true,
            targetType: "SecureShare",
            details: $"Deleted {deletedCount} expired shares. Unused expired shares={expiredUnusedCount}.",
            cancellationToken: stoppingToken);
        await _usageMetricsService.RecordAsync(DbUsageMetricsService.ExpiredDeletedKey, "system", "cleanup-service", deletedCount, details: "Expired shares deleted during cleanup.", cancellationToken: stoppingToken);

        if (expiredUnusedCount > 0)
        {
            await _usageMetricsService.RecordAsync(DbUsageMetricsService.ExpiredUnusedDeletedKey, "system", "cleanup-service", expiredUnusedCount, details: "Expired unused shares deleted during cleanup.", cancellationToken: stoppingToken);
        }
    }

    private async Task DeleteExpiredInformationRequestsAsync(DateTime nowUtc, CancellationToken stoppingToken)
    {
        var deletedCount = await _informationRequestStore.DeleteExpiredInformationRequestsAsync(nowUtc, stoppingToken);
        if (deletedCount <= 0)
        {
            return;
        }

        await _auditLogger.LogAsync(
            "system",
            "cleanup-service",
            "cleanup.expired-information-requests",
            true,
            targetType: "InformationRequest",
            details: $"Deleted {deletedCount} expired information requests.",
            cancellationToken: stoppingToken);
        await _usageMetricsService.RecordAsync(DbUsageMetricsService.ExpiredInformationRequestsDeletedKey, "system", "cleanup-service", deletedCount, details: "Expired information requests deleted during cleanup.", cancellationToken: stoppingToken);
    }
}
