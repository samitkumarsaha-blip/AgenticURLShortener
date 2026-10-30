using UrlShortener.Orchestration;

namespace UrlShortener.UnitTests;

public sealed class WorkflowOrchestratorTests
{
    [Fact]
    public async Task RequiresArchitectureAndReleaseApprovals()
    {
        var orchestrator = new WorkflowOrchestrator([
            new RequirementsAgent(),
            new ArchitectureAgent(),
            new ImplementationAgent(),
            new QaValidationAgent(),
            new ObservabilityAgent(),
            new ReleaseReadinessAgent()
        ]);

        var run = await orchestrator.StartAsync(new StartWorkflowRequest("Build a secure URL shortener"), CancellationToken.None);

        Assert.Equal(WorkflowStatus.AwaitingApproval, run.Status);
        Assert.Equal(WorkflowStage.Architecture, run.CurrentStage);
        Assert.Contains("normalize-requirements", run.CompletedTasks);
        Assert.Contains("produce-architecture", run.CompletedTasks);
        Assert.Contains(run.Artifacts, artifact => artifact.Name == "normalized-requirements");
        Assert.Contains(run.Artifacts, artifact => artifact.Name == "architecture-decision-record");
        Assert.Contains(run.Decisions, decision => decision.Decision.Contains("provider-neutral"));

        run = (await orchestrator.ApproveAsync(run.Id,
            new ApprovalRequest("Architecture", true, "reviewer", "Approved for implementation"), CancellationToken.None))!;

        Assert.Equal(WorkflowStatus.AwaitingApproval, run.Status);
        Assert.Equal(WorkflowStage.ReleaseReadiness, run.CurrentStage);
        Assert.Contains("implement-change", run.CompletedTasks);
        Assert.Contains("validate-change", run.CompletedTasks);
        Assert.Contains(run.Artifacts, artifact => artifact.Name == "implementation-package");
        Assert.Contains(run.Validations, validation => validation.Name == "failure-recovery" && validation.Passed);

        run = (await orchestrator.ApproveAsync(run.Id,
            new ApprovalRequest("ReleaseReadiness", true, "reviewer", "Approved for release"), CancellationToken.None))!;

        Assert.Equal(WorkflowStatus.Completed, run.Status);
        Assert.Contains(run.Artifacts, artifact => artifact.Name == "release-runbook");
    }

    [Fact]
    public async Task StopsAfterThreeAttemptsWhenAnAgentFails()
    {
        var orchestrator = new WorkflowOrchestrator([
            new FailingAgent(WorkflowStage.Requirements),
            new ArchitectureAgent(),
            new ImplementationAgent(),
            new QaValidationAgent(),
            new ObservabilityAgent(),
            new ReleaseReadinessAgent()
        ]);

        var run = await orchestrator.StartAsync(new StartWorkflowRequest("Fail safely"), CancellationToken.None);

        var task = Assert.Single(run.Tasks, task => task.Id == "requirements");
        Assert.Equal(WorkflowStatus.SafeStopped, run.Status);
        Assert.Equal(3, task.Attempts);
        Assert.Equal(3, run.RetryCount);
        Assert.Contains("safe-stop", run.AuditTrail[^1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecutesParallelReviewsAndSupportsRollbackAndReplan()
    {
        var orchestrator = CreateOrchestrator();
        var run = await orchestrator.StartAsync(new StartWorkflowRequest("Build a URL shortener"), CancellationToken.None);
        await orchestrator.ApproveAsync(run.Id, new ApprovalRequest("Architecture", true, "reviewer", "Approved"), CancellationToken.None);

        Assert.Equal(WorkflowStatus.AwaitingApproval, run.Status);
        Assert.Equal(WorkflowTaskStatus.Succeeded, Assert.Single(run.Tasks, task => task.Id == "security-review").Status);
        Assert.Equal(WorkflowTaskStatus.Succeeded, Assert.Single(run.Tasks, task => task.Id == "performance-review").Status);
        Assert.NotEmpty(run.Checkpoints);

        await orchestrator.RollbackAsync(run.Id, CancellationToken.None);
        Assert.Equal(1, run.RollbackCount);
        Assert.Contains("rolled back", run.AuditTrail[^1], StringComparison.OrdinalIgnoreCase);

        var completedBeforeReplan = run.Tasks.Count(task => task.Status == WorkflowTaskStatus.Succeeded);
        await orchestrator.ReplanAsync(run.Id, new ReplanRequest("Updated requirement", "QA defect found"), CancellationToken.None);
        Assert.Equal(2, run.PlanVersion);
        Assert.True(run.Tasks.Count(task => task.Status == WorkflowTaskStatus.Succeeded) >= completedBeforeReplan);
    }

    private static WorkflowOrchestrator CreateOrchestrator() => new([
        new RequirementsAgent(),
        new ArchitectureAgent(),
        new ImplementationAgent(),
        new QaValidationAgent(),
        new ObservabilityAgent(),
        new ReleaseReadinessAgent()
    ]);

    private sealed class FailingAgent(WorkflowStage stage) : IWorkflowAgent
    {
        public WorkflowStage Stage { get; } = stage;

        public Task ExecuteAsync(WorkflowRun run, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("synthetic failure");
    }
}