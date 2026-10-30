namespace UrlShortener.Domain;

public sealed class ClickEvent
{
    private ClickEvent() { }

    public ClickEvent(string shortCode, DateTimeOffset occurredAt, string? referrer, string? userAgent, string? clientIp)
    {
        ShortCode = shortCode;
        OccurredAt = occurredAt;
        Referrer = referrer;
        UserAgent = userAgent;
        ClientIp = clientIp;
    }

    public long Id { get; private set; }
    public string ShortCode { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Referrer { get; private set; }
    public string? UserAgent { get; private set; }
    public string? ClientIp { get; private set; }
}