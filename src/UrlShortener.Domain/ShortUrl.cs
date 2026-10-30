namespace UrlShortener.Domain;

public sealed class ShortUrl
{
    private ShortUrl() { }

    public ShortUrl(string code, Uri destination, DateTimeOffset createdAt, DateTimeOffset? expiresAt = null, string? alias = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A short code is required.", nameof(code));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Expiration must be later than creation time.", nameof(expiresAt));
        }

        Code = code;
        Destination = destination;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Alias = alias;
    }

    public string Code { get; private set; } = string.Empty;
    public Uri Destination { get; private set; } = null!;
    public string? Alias { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public long ClickCount { get; private set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;

    public void RegisterClick() => ClickCount++;
}