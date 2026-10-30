using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Telemetry;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/operations")]
public sealed class OperationsController(RequestMetrics metrics) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });

    [HttpGet("metrics")]
    public IActionResult Metrics() => Ok(metrics.Snapshot());
}