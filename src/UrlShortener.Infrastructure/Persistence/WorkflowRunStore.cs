using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Orchestration;

namespace UrlShortener.Infrastructure.Persistence;

public sealed class WorkflowRunStore(IDbContextFactory<ShortUrlDbContext> contextFactory) : IWorkflowRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.WorkflowRuns.SingleOrDefaultAsync(item => item.Id == run.Id, cancellationToken);
        if (record is null)
        {
            record = new WorkflowRunRecord { Id = run.Id };
            await context.WorkflowRuns.AddAsync(record, cancellationToken);
        }

        record.Payload = JsonSerializer.Serialize(run, JsonOptions);
        record.UpdatedAt = run.UpdatedAt;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowRun?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.WorkflowRuns.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return record is null ? null : JsonSerializer.Deserialize<WorkflowRun>(record.Payload, JsonOptions);
    }
}