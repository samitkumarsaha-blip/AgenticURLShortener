using System.Collections.Concurrent;

namespace UrlShortener.Orchestration;

public interface IWorkflowOrchestrator
{
    Task<WorkflowRun> StartAsync(StartWorkflowRequest request, CancellationToken cancellationToken);
    Task<WorkflowRun?> AdvanceStepAsync(Guid id, CancellationToken cancellationToken);
    Task<WorkflowRun?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<WorkflowRun?> ApproveAsync(Guid id, ApprovalRequest request, CancellationToken cancellationToken);
    Task<WorkflowRun?> ReplanAsync(Guid id, ReplanRequest request, CancellationToken cancellationToken);
    Task<WorkflowRun?> RollbackAsync(Guid id, CancellationToken cancellationToken);
}

public interface IWorkflowRunStore
{
    Task SaveAsync(WorkflowRun run, CancellationToken cancellationToken);
    Task<WorkflowRun?> LoadAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class WorkflowOrchestrator(IEnumerable<IWorkflowAgent> agents, IWorkflowRunStore? store = null) : IWorkflowOrchestrator
{
    private const int MaxAttempts = 3;
    private readonly ConcurrentDictionary<Guid, WorkflowRun> runs = new();
    private readonly IReadOnlyDictionary<WorkflowStage, IWorkflowAgent> agents = agents.ToDictionary(agent => agent.Stage);
    private readonly IWorkflowRunStore? store = store;

    public async Task<WorkflowRun> StartAsync(StartWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Requirements))
        {
            throw new ArgumentException("Requirements are required.", nameof(request));
        }

        var run = new WorkflowRun
        {
            Requirements = request.Requirements,
            Scenario = request.Scenario,
            StepMode = request.StepMode
        };
        BuildPlan(run);
        runs[run.Id] = run;
        AddAudit(run, $"workflow started as {run.Scenario}; plan version {run.PlanVersion}.");
        await ExecuteReadyTasksAsync(run, cancellationToken);
        await PersistAsync(run, cancellationToken);
        return run;
    }

    public async Task<WorkflowRun?> AdvanceStepAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!runs.TryGetValue(id, out var run))
        {
            return null;
        }

        if (!run.StepMode || run.Status != WorkflowStatus.AwaitingStep)
        {
            throw new InvalidOperationException("The workflow is not waiting for a manual step.");
        }

        run.Status = WorkflowStatus.Running;
        await ExecuteReadyTasksAsync(run, cancellationToken);
        await PersistAsync(run, cancellationToken);
        return run;
    }

    public async Task<WorkflowRun?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        runs.GetValueOrDefault(id) ?? (store is null ? null : await store.LoadAsync(id, cancellationToken));

    public async Task<WorkflowRun?> ApproveAsync(Guid id, ApprovalRequest request, CancellationToken cancellationToken)
    {
        if (!runs.TryGetValue(id, out var run))
        {
            return null;
        }

        if (!Enum.TryParse<WorkflowStage>(request.Stage, true, out var stage) || run.CurrentStage != stage)
        {
            throw new ArgumentException("The approval does not match the current workflow gate.", nameof(request));
        }

        run.Approvals.Add($"{request.Stage}: {(request.Approved ? "approved" : "rejected")} by {request.Reviewer} - {request.Rationale}");
        AddAudit(run, $"approval decision recorded for {request.Stage}.");
        if (!request.Approved)
        {
            run.Status = WorkflowStatus.Rejected;
            await PersistAsync(run, cancellationToken);
            return run;
        }

        run.ApprovedStages.Add(stage);
        run.Status = WorkflowStatus.Running;
        if (run.StepMode)
        {
            SetManualStepState(run);
            await PersistAsync(run, cancellationToken);
            return run;
        }

        await ExecuteReadyTasksAsync(run, cancellationToken);
        await PersistAsync(run, cancellationToken);
        return run;
    }

    public async Task<WorkflowRun?> ReplanAsync(Guid id, ReplanRequest request, CancellationToken cancellationToken)
    {
        if (!runs.TryGetValue(id, out var run))
        {
            return null;
        }

        var impactedStage = DetermineImpactedStage(run, request.Rationale);
        run.Status = WorkflowStatus.Replanning;
        run.PlanVersion++;
        run.Requirements = request.Requirements;
        foreach (var task in run.Tasks.Where(task => task.Stage >= impactedStage))
        {
            task.Status = WorkflowTaskStatus.Invalidated;
            run.ApprovedStages.Remove(task.Stage);
        }

        AddAudit(run, $"replanned from {impactedStage}; reason: {request.Rationale}; preserved unaffected tasks.");
        run.Status = WorkflowStatus.Running;
        await ExecuteReadyTasksAsync(run, cancellationToken);
        await PersistAsync(run, cancellationToken);
        return run;
    }

    public async Task<WorkflowRun?> RollbackAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!runs.TryGetValue(id, out var run))
        {
            return null;
        }

        var checkpoint = run.Checkpoints.LastOrDefault();
        if (checkpoint is null)
        {
            throw new InvalidOperationException("No rollback checkpoint is available.");
        }

        foreach (var task in run.Tasks)
        {
            if (checkpoint.TaskStatuses.TryGetValue(task.Id, out var status))
            {
                task.Status = status;
            }
        }

        run.RollbackCount++;
        run.Status = WorkflowStatus.Running;
        AddAudit(run, $"rolled back to checkpoint {checkpoint.Name}; rollback count {run.RollbackCount}.");
        await ExecuteReadyTasksAsync(run, cancellationToken);
        await PersistAsync(run, cancellationToken);
        return run;
    }

    private Task PersistAsync(WorkflowRun run, CancellationToken cancellationToken) =>
        store is null ? Task.CompletedTask : store.SaveAsync(run, cancellationToken);

    private async Task ExecuteReadyTasksAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        if (run.Status is WorkflowStatus.Rejected or WorkflowStatus.SafeStopped or WorkflowStatus.Completed)
        {
            return;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readyTasks = GetReadyTasks(run);
            if (readyTasks.Count == 0)
            {
                run.Status = run.Tasks.All(task => task.Status == WorkflowTaskStatus.Succeeded) ? WorkflowStatus.Completed : run.Status;
                return;
            }

            if (run.StepMode)
            {
                var task = readyTasks[0];
                await ExecuteTaskWithRetryAsync(run, task, cancellationToken);
                if (run.Status == WorkflowStatus.SafeStopped)
                {
                    return;
                }

                if (task.Status == WorkflowTaskStatus.Succeeded && IsApprovalGate(task) && !run.ApprovedStages.Contains(task.Stage))
                {
                    run.CurrentStage = task.Stage;
                    run.Status = WorkflowStatus.AwaitingApproval;
                    return;
                }

                SetManualStepState(run);
                return;
            }

            await Task.WhenAll(readyTasks.Select(task => ExecuteTaskWithRetryAsync(run, task, cancellationToken)));
            if (run.Status == WorkflowStatus.SafeStopped)
            {
                return;
            }

            var completedGate = readyTasks.FirstOrDefault(task =>
                task.Status == WorkflowTaskStatus.Succeeded &&
                (task.Stage is WorkflowStage.Architecture or WorkflowStage.ReleaseReadiness) &&
                !run.ApprovedStages.Contains(task.Stage));
            if (completedGate is not null)
            {
                run.CurrentStage = completedGate.Stage;
                run.Status = WorkflowStatus.AwaitingApproval;
                return;
            }
        }
    }

    private static List<WorkflowTask> GetReadyTasks(WorkflowRun run) => run.Tasks.Where(task =>
        (task.Status is WorkflowTaskStatus.Pending or WorkflowTaskStatus.Invalidated) &&
        task.Dependencies.All(dependency => run.Tasks.Single(candidate => candidate.Id == dependency).Status == WorkflowTaskStatus.Succeeded)).ToList();

    private static bool IsApprovalGate(WorkflowTask task) =>
        task.Stage is WorkflowStage.Architecture or WorkflowStage.ReleaseReadiness;

    private static void SetManualStepState(WorkflowRun run)
    {
        var nextTask = GetReadyTasks(run).FirstOrDefault();
        if (nextTask is null)
        {
            run.Status = run.Tasks.All(task => task.Status == WorkflowTaskStatus.Succeeded)
                ? WorkflowStatus.Completed
                : WorkflowStatus.SafeStopped;
            return;
        }

        run.CurrentStage = nextTask.Stage;
        run.Status = WorkflowStatus.AwaitingStep;
    }

    private async Task ExecuteTaskWithRetryAsync(WorkflowRun run, WorkflowTask task, CancellationToken cancellationToken)
    {
        task.Status = WorkflowTaskStatus.Running;
        if (task.Stage == WorkflowStage.Implementation)
        {
            CreateCheckpoint(run, "before-implementation");
        }

        while (task.Attempts < MaxAttempts)
        {
            task.Attempts++;
            try
            {
                await agents[task.Stage].ExecuteAsync(run, cancellationToken);
                task.Status = WorkflowTaskStatus.Succeeded;
                AddAudit(run, $"task {task.Id} succeeded on attempt {task.Attempts}.");
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                task.Error = exception.Message;
                run.RetryCount++;
                AddAudit(run, $"task {task.Id} failed on attempt {task.Attempts}: {exception.Message}.");
            }
        }

        task.Status = WorkflowTaskStatus.Failed;
        run.Status = WorkflowStatus.SafeStopped;
        run.SafeStopReason = $"Task {task.Id} exceeded the {MaxAttempts}-attempt retry limit.";
        AddAudit(run, $"safe-stop activated: {run.SafeStopReason}");
    }

    private static void BuildPlan(WorkflowRun run)
    {
        run.Tasks.Add(new WorkflowTask { Id = "requirements", Stage = WorkflowStage.Requirements });
        run.Tasks.Add(new WorkflowTask { Id = "architecture", Stage = WorkflowStage.Architecture, Dependencies = ["requirements"] });
        run.Tasks.Add(new WorkflowTask { Id = "implementation", Stage = WorkflowStage.Implementation, Dependencies = ["architecture"] });
        run.Tasks.Add(new WorkflowTask { Id = "security-review", Stage = WorkflowStage.QaValidation, Dependencies = ["implementation"] });
        run.Tasks.Add(new WorkflowTask { Id = "performance-review", Stage = WorkflowStage.Observability, Dependencies = ["implementation"] });
        run.Tasks.Add(new WorkflowTask { Id = "qa-validation", Stage = WorkflowStage.QaValidation, Dependencies = ["security-review", "performance-review"] });
        run.Tasks.Add(new WorkflowTask { Id = "observability", Stage = WorkflowStage.Observability, Dependencies = ["qa-validation"] });
        run.Tasks.Add(new WorkflowTask { Id = "release-readiness", Stage = WorkflowStage.ReleaseReadiness, Dependencies = ["observability"] });
    }

    private static void CreateCheckpoint(WorkflowRun run, string name)
    {
        run.Checkpoints.Add(new WorkflowCheckpoint
        {
            Name = name,
            TaskStatuses = run.Tasks.ToDictionary(task => task.Id, task => task.Status)
        });
        AddAudit(run, $"checkpoint created: {name}.");
    }

    private static WorkflowStage DetermineImpactedStage(WorkflowRun run, string rationale) =>
        rationale.Contains("architecture", StringComparison.OrdinalIgnoreCase) || rationale.Contains("requirement", StringComparison.OrdinalIgnoreCase)
            ? WorkflowStage.Requirements
            : run.CurrentStage;

    private static void AddAudit(WorkflowRun run, string message)
    {
        lock (run.SyncRoot)
        {
            run.AuditTrail.Add($"{DateTimeOffset.UtcNow:O}: {message}");
            run.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
