using System.Text.Json.Serialization;

namespace UrlShortener.Orchestration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowStage
{
    Requirements,
    Architecture,
    Implementation,
    QaValidation,
    Observability,
    ReleaseReadiness
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowStatus
{
    AwaitingApproval,
    Running,
    Replanning,
    Completed,
    Rejected,
    SafeStopped
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowTaskStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Invalidated,
    RolledBack
}

public sealed record StartWorkflowRequest(string Requirements, string Scenario = "greenfield");
public sealed record ApprovalRequest(string Stage, bool Approved, string Reviewer, string Rationale);
public sealed record ReplanRequest(string Requirements, string Rationale);

public sealed class WorkflowTask
{
    public string Id { get; init; } = string.Empty;
    public WorkflowStage Stage { get; init; }
    public List<string> Dependencies { get; init; } = [];
    public WorkflowTaskStatus Status { get; set; } = WorkflowTaskStatus.Pending;
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

public sealed class WorkflowCheckpoint
{
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, WorkflowTaskStatus> TaskStatuses { get; init; } = [];
}

public sealed record WorkflowArtifact(string Name, string Description, WorkflowStage ProducedBy);
public sealed record WorkflowDecision(string Decision, string Rationale, WorkflowStage MadeAt);
public sealed record WorkflowValidation(string Name, bool Passed, string Evidence, WorkflowStage ValidatedAt);

public sealed class WorkflowRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Requirements { get; set; } = string.Empty;
    public string Scenario { get; set; } = "greenfield";
    public WorkflowStage CurrentStage { get; set; } = WorkflowStage.Requirements;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;
    public int PlanVersion { get; set; } = 1;
    public List<string> CompletedTasks { get; set; } = [];
    public List<string> AuditTrail { get; set; } = [];
    public List<string> Approvals { get; set; } = [];
    public List<WorkflowTask> Tasks { get; set; } = [];
    public List<WorkflowCheckpoint> Checkpoints { get; set; } = [];
    public List<WorkflowArtifact> Artifacts { get; set; } = [];
    public List<WorkflowDecision> Decisions { get; set; } = [];
    public List<WorkflowValidation> Validations { get; set; } = [];
    public List<string> Risks { get; set; } = [];
    public int RetryCount { get; set; }
    public int RollbackCount { get; set; }
    public string? SafeStopReason { get; set; }
    public HashSet<WorkflowStage> ApprovedStages { get; set; } = [];
    [JsonIgnore]
    public object SyncRoot { get; } = new();
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}