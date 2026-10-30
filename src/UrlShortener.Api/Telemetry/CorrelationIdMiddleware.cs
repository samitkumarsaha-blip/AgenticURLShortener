using System.Diagnostics;

namespace UrlShortener.Api.Telemetry;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, RequestMetrics metrics)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.TraceIdentifier = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            metrics.Record(context.Request.Path, context.Response.StatusCode, stopwatch.Elapsed);
            logger.LogInformation("HTTP request completed {Method} {Path} {StatusCode} {DurationMs}ms {CorrelationId}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.Elapsed.TotalMilliseconds, correlationId);
        }
    }
}