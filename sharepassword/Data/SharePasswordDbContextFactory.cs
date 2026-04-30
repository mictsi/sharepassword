using Microsoft.EntityFrameworkCore;

namespace SharePassword.Data;

public interface ISharePasswordDbContextFactory
{
    Task<SharePasswordDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}

internal sealed class SharePasswordDbContextFactory<TContext> : ISharePasswordDbContextFactory
    where TContext : SharePasswordDbContext
{
    private readonly IDbContextFactory<TContext> _factory;

    public SharePasswordDbContextFactory(IDbContextFactory<TContext> factory)
    {
        _factory = factory;
    }

    public async Task<SharePasswordDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await _factory.CreateDbContextAsync(cancellationToken);
    }
}