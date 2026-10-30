namespace UrlShortener.Infrastructure.Persistence;

public sealed class WorkflowRunRecord
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}