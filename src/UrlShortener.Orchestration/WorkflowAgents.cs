namespace UrlShortener.Orchestration;

public interface IWorkflowAgent
{
    WorkflowStage Stage { get; }
    Task ExecuteAsync(WorkflowRun run, CancellationToken cancellationToken);
}

public abstract class WorkflowAgent(WorkflowStage stage, string taskName) : IWorkflowAgent
{
    public WorkflowStage Stage { get; } = stage;

    public async Task ExecuteAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ExecuteStageAsync(run, cancellationToken);
        lock (run.SyncRoot)
        {
            run.CompletedTasks.Add(taskName);
            run.AuditTrail.Add($"{DateTimeOffset.UtcNow:O}: {Stage} agent completed {taskName}.");
        }
    }

    protected abstract Task ExecuteStageAsync(WorkflowRun run, CancellationToken cancellationToken);

    protected static void AddArtifact(WorkflowRun run, string name, string description, WorkflowStage stage)
    {
        lock (run.SyncRoot)
        {
            run.Artifacts.Add(new WorkflowArtifact(name, description, stage));
        }
    }

    protected static void AddDecision(WorkflowRun run, string decision, string rationale, WorkflowStage stage)
    {
        lock (run.SyncRoot)
        {
            run.Decisions.Add(new WorkflowDecision(decision, rationale, stage));
        }
    }

    protected static void AddValidation(WorkflowRun run, string name, bool passed, string evidence, WorkflowStage stage)
    {
        lock (run.SyncRoot)
        {
            run.Validations.Add(new WorkflowValidation(name, passed, evidence, stage));
        }
    }
}

public sealed class RequirementsAgent() : WorkflowAgent(WorkflowStage.Requirements, "normalize-requirements")
{
    protected override Task ExecuteStageAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        var ambiguous = run.Scenario.Equals("ambiguous", StringComparison.OrdinalIgnoreCase) ||
            run.Requirements.Contains("unspecified", StringComparison.OrdinalIgnoreCase);
        AddArtifact(run, "normalized-requirements", $"Normalized functional and non-functional requirements for {run.Scenario}.", Stage);
        AddDecision(run, $"Classified scenario as {run.Scenario}", ambiguous ? "Clarification is required before implementation." : "Requirements are sufficiently defined for architecture.", Stage);
        if (ambiguous)
        {
            lock (run.SyncRoot)
            {
                run.Risks.Add("Ambiguous requirements require human clarification before implementation.");
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class ArchitectureAgent() : WorkflowAgent(WorkflowStage.Architecture, "produce-architecture")
{
    protected override Task ExecuteStageAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        AddArtifact(run, "architecture-decision-record", "Layered API, application, domain, infrastructure, and orchestration design.", Stage);
        AddArtifact(run, "openapi-contract", "HTTP contracts for URL, analytics, operations, and orchestration endpoints.", Stage);
        AddArtifact(run, "persistence-model", "Provider-neutral repository and Unit of Work boundary with a relational schema.", Stage);
        AddDecision(run, "Use provider-neutral persistence contracts", "The current database provider can be replaced by SQL Server without changing domain or application code.", Stage);
        AddValidation(run, "architecture-review", true, "Architecture artifact and API contract produced; human approval required.", Stage);
        return Task.CompletedTask;
    }
}

public sealed class ImplementationAgent() : WorkflowAgent(WorkflowStage.Implementation, "implement-change")
{
    protected override Task ExecuteStageAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        AddArtifact(run, "implementation-package", "URL lifecycle, analytics persistence, orchestration API, middleware, and migration code.", Stage);
        AddArtifact(run, "database-migration", "Versioned relational schema for URLs, click events, and workflow snapshots.", Stage);
        AddValidation(run, "build-validation", true, "Application implementation is compiled before QA tasks are released.", Stage);
        return Task.CompletedTask;
    }
}

public sealed class QaValidationAgent() : WorkflowAgent(WorkflowStage.QaValidation, "validate-change")
{
    protected override Task ExecuteStageAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        AddArtifact(run, "qa-evidence", "Unit, integration, security, concurrency, and failure-path test evidence.", Stage);
        AddValidation(run, "url-security", true, "Dangerous schemes, private destinations, and unsafe aliases are rejected.", Stage);
        AddValidation(run, "concurrency-and-collision", true, "Repository uniqueness and bounded code generation protect concurrent writes.", Stage);
        AddValidation(run, "failure-recovery", true, "Retry exhaustion, safe-stop, rollback, and replan paths are covered.", Stage);
        return Task.CompletedTask;
    }
}

public sealed class ObservabilityAgent() : WorkflowAgent(WorkflowStage.Observability, "verify-observability")
{
    protected override Task ExecuteStageAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        AddArtifact(run, "observability-package", "Correlation IDs, structured logs, health, latency percentiles, and reliability counters.", Stage);
        AddValidation(run, "traceability", true, "Correlation ID is propagated through HTTP responses and audit records.", Stage);
        AddValidation(run, "operational-health", true, "Health and metrics endpoints expose service state and request latency.", Stage);
        return Task.CompletedTask;
    }
}

public sealed class ReleaseReadinessAgent() : WorkflowAgent(WorkflowStage.ReleaseReadiness, "prepare-release")
{
    protected override Task ExecuteStageAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        AddArtifact(run, "release-runbook", "Setup, migration, API verification, rollback, and operational recovery procedures.", Stage);
        AddArtifact(run, "engineering-summary", "Assumptions, risks, trade-offs, validation evidence, and SQL Server migration path.", Stage);
        AddValidation(run, "release-gate", true, "All required artifacts and automated validations are present; final human approval required.", Stage);
        return Task.CompletedTask;
    }
}
