using System.Security.Cryptography;
using UrlShortener.Domain;

namespace UrlShortener.Application;

public sealed class ShortUrlService(IShortUrlRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IShortUrlService
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public async Task<ShortUrlResponse> CreateAsync(CreateShortUrlRequest request, CancellationToken cancellationToken)
    {
        if (!UrlValidator.TryValidate(request.Url, out var destination, out var urlError))
        {
            throw new ArgumentException(urlError, nameof(request));
        }

        if (request.Alias is not null && !UrlValidator.IsValidAlias(request.Alias))
        {
            throw new ArgumentException("Aliases may contain only ASCII letters, numbers, '-' and '_'.", nameof(request));
        }

        if (request.TtlMinutes is <= 0 or > 525600)
        {
            throw new ArgumentException("TTL must be between 1 minute and 365 days.", nameof(request));
        }

        if (request.Alias is not null && await repository.AliasExistsAsync(request.Alias, cancellationToken))
        {
            throw new InvalidOperationException("The requested alias is already in use.");
        }

        var now = timeProvider.GetUtcNow();
        var shortUrl = new ShortUrl(CreateCode(), destination!, now,
            request.TtlMinutes is null ? null : now.AddMinutes(request.TtlMinutes.Value), request.Alias);
        await repository.AddAsync(shortUrl, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return new ShortUrlResponse(shortUrl.Code, $"/{shortUrl.Code}", shortUrl.CreatedAt, shortUrl.ExpiresAt, shortUrl.ClickCount);
    }

    public async Task<ShortUrl?> ResolveAsync(string code, ClickContext context, CancellationToken cancellationToken)
    {
        var shortUrl = await repository.FindAsync(code, cancellationToken);
        if (shortUrl is null || shortUrl.IsExpired(timeProvider.GetUtcNow()))
        {
            return null;
        }

        shortUrl.RegisterClick();
        await repository.AddClickAsync(new ClickEvent(code, timeProvider.GetUtcNow(), context.Referrer, context.UserAgent, context.ClientIp), cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return shortUrl;
    }

    public async Task<AnalyticsResponse?> GetAnalyticsAsync(string code, CancellationToken cancellationToken)
    {
        var shortUrl = await repository.FindAsync(code, cancellationToken);
        if (shortUrl is null)
        {
            return null;
        }

        var clicks = await repository.GetClicksAsync(code, cancellationToken);
        return new AnalyticsResponse(code, clicks.Count,
            clicks.Take(100).Select(click => new ClickEventResponse(click.OccurredAt, click.Referrer, click.UserAgent)).ToList());
    }

    private static string CreateCode()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var code = new char[8];
        for (var index = 0; index < code.Length; index++)
        {
            code[index] = Alphabet[bytes[index] % Alphabet.Length];
        }

        return new string(code);
    }
}