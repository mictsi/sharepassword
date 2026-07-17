using Microsoft.EntityFrameworkCore;

namespace Sekura.Data;

public interface ISekuraDbContextFactory
{
    Task<SekuraDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}

internal sealed class SekuraDbContextFactory<TContext> : ISekuraDbContextFactory
    where TContext : SekuraDbContext
{
    private readonly DbContextOptions<TContext> _options;

    public SekuraDbContextFactory(DbContextOptions<TContext> options)
    {
        _options = options;
    }

    public Task<SekuraDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = Activator.CreateInstance(typeof(TContext), _options)
            ?? throw new InvalidOperationException($"Could not create DbContext '{typeof(TContext).Name}'.");

        return Task.FromResult((SekuraDbContext)dbContext);
    }
}
