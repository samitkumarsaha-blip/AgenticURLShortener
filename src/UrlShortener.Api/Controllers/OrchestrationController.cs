using Microsoft.AspNetCore.Mvc;
using UrlShortener.Orchestration;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/orchestration/runs")]
public sealed class OrchestrationController(IWorkflowOrchestrator orchestrator) : ControllerBase
{
    [HttpGet("/api/orchestration/scenarios")]
    public ActionResult<IReadOnlyList<ScenarioDefinition>> Scenarios() => Ok(ScenarioCatalog.All);

    [HttpPost("/api/orchestration/scenarios/{scenario}/runs")]
    public async Task<ActionResult<WorkflowRun>> StartScenario(string scenario, CancellationToken cancellationToken)
    {
        var definition = ScenarioCatalog.Find(scenario);
        if (definition is null)
        {
            return NotFound(new ProblemDetails { Title = "Unknown scenario", Detail = scenario });
        }

        return Accepted(await orchestrator.StartAsync(new StartWorkflowRequest(definition.Requirement, definition.Name), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowRun>> Start(StartWorkflowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Accepted(await orchestrator.StartAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid workflow request", Detail = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowRun>> Get(Guid id, CancellationToken cancellationToken) =>
        await orchestrator.GetAsync(id, cancellationToken) is { } run ? Ok(run) : NotFound();

    [HttpPost("{id:guid}/steps")]
    public async Task<ActionResult<WorkflowRun>> AdvanceStep(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await orchestrator.AdvanceStepAsync(id, cancellationToken) is { } run
                ? Ok(run)
                : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Title = "Step unavailable", Detail = exception.Message });
        }
    }

    [HttpPost("{id:guid}/approvals")]
    public async Task<ActionResult<WorkflowRun>> Approve(Guid id, ApprovalRequest request, CancellationToken cancellationToken) =>
        await orchestrator.ApproveAsync(id, request, cancellationToken) is { } run ? Ok(run) : NotFound();

    [HttpPost("{id:guid}/replan")]
    public async Task<ActionResult<WorkflowRun>> Replan(Guid id, ReplanRequest request, CancellationToken cancellationToken) =>
        await orchestrator.ReplanAsync(id, request, cancellationToken) is { } run ? Ok(run) : NotFound();

    [HttpPost("{id:guid}/rollback")]
    public async Task<ActionResult<WorkflowRun>> Rollback(Guid id, CancellationToken cancellationToken) =>
        await orchestrator.RollbackAsync(id, cancellationToken) is { } run ? Ok(run) : NotFound();
}