using Microsoft.EntityFrameworkCore;
using UrlShortener.Application;
using UrlShortener.Domain;

namespace UrlShortener.Infrastructure;

public sealed class ShortUrlRepository(ShortUrlDbContext dbContext) : IShortUrlRepository
{
    public Task<ShortUrl?> FindAsync(string code, CancellationToken cancellationToken) =>
        dbContext.ShortUrls.SingleOrDefaultAsync(shortUrl => shortUrl.Code == code, cancellationToken);

    public Task<bool> AliasExistsAsync(string alias, CancellationToken cancellationToken) =>
        dbContext.ShortUrls.AnyAsync(shortUrl => shortUrl.Alias == alias, cancellationToken);

    public async Task AddAsync(ShortUrl shortUrl, CancellationToken cancellationToken) =>
        await dbContext.ShortUrls.AddAsync(shortUrl, cancellationToken);

    public async Task AddClickAsync(ClickEvent clickEvent, CancellationToken cancellationToken) =>
        await dbContext.ClickEvents.AddAsync(clickEvent, cancellationToken);

    public async Task<IReadOnlyList<ClickEvent>> GetClicksAsync(string code, CancellationToken cancellationToken) =>
        (await dbContext.ClickEvents.Where(click => click.ShortCode == code).ToListAsync(cancellationToken))
            .OrderByDescending(click => click.OccurredAt).ToList();

}