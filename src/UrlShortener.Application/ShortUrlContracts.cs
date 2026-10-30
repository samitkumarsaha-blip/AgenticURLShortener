using UrlShortener.Domain;

namespace UrlShortener.Application;

public sealed record CreateShortUrlRequest(string Url, string? Alias = null, int? TtlMinutes = null);
public sealed record ShortUrlResponse(string Code, string Url, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, long ClickCount);
public sealed record ClickContext(string? Referrer, string? UserAgent, string? ClientIp);
public sealed record AnalyticsResponse(string Code, long TotalClicks, IReadOnlyList<ClickEventResponse> RecentClicks);
public sealed record ClickEventResponse(DateTimeOffset OccurredAt, string? Referrer, string? UserAgent);

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IShortUrlRepository
{
    Task<ShortUrl?> FindAsync(string code, CancellationToken cancellationToken);
    Task<bool> AliasExistsAsync(string alias, CancellationToken cancellationToken);
    Task AddAsync(ShortUrl shortUrl, CancellationToken cancellationToken);
    Task AddClickAsync(ClickEvent clickEvent, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClickEvent>> GetClicksAsync(string code, CancellationToken cancellationToken);
}

public interface IShortUrlService
{
    Task<ShortUrlResponse> CreateAsync(CreateShortUrlRequest request, CancellationToken cancellationToken);
    Task<ShortUrl?> ResolveAsync(string code, ClickContext context, CancellationToken cancellationToken);
    Task<AnalyticsResponse?> GetAnalyticsAsync(string code, CancellationToken cancellationToken);
}