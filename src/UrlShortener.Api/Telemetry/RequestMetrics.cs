using System.Collections.Concurrent;

namespace UrlShortener.Api.Telemetry;

public sealed class RequestMetrics
{
    private readonly ConcurrentBag<double> latencies = [];
    private long totalRequests;
    private long successfulRequests;
    private long failedRequests;

    public void Record(string path, int statusCode, TimeSpan duration)
    {
        Interlocked.Increment(ref totalRequests);
        if (statusCode is >= 200 and < 400)
        {
            Interlocked.Increment(ref successfulRequests);
        }
        else
        {
            Interlocked.Increment(ref failedRequests);
        }

        latencies.Add(duration.TotalMilliseconds);
    }

    public object Snapshot()
    {
        var values = latencies.ToArray();
        Array.Sort(values);
        return new
        {
            totalRequests,
            successfulRequests,
            failedRequests,
            successRate = totalRequests == 0 ? 1 : (double)successfulRequests / totalRequests,
            p50LatencyMs = Percentile(values, 0.50),
            p95LatencyMs = Percentile(values, 0.95),
            p99LatencyMs = Percentile(values, 0.99)
        };
    }

    private static double Percentile(double[] values, double percentile) =>
        values.Length == 0 ? 0 : values[Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * percentile) - 1)];
}