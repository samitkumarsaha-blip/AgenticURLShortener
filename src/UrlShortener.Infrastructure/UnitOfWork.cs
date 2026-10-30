using UrlShortener.Application;

namespace UrlShortener.Infrastructure;

public sealed class UnitOfWork(ShortUrlDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}