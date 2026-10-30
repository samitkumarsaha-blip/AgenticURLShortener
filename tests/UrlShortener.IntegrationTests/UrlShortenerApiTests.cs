using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using UrlShortener.Orchestration;
using UrlShortener.Application;

namespace UrlShortener.IntegrationTests;

public sealed class UrlShortenerApiTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"urlshortener-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Shortener"] = $"Data Source={databasePath}" })));
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        client.Dispose();
        factory.Dispose();
        File.Delete(databasePath);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreatesRedirectsAndRecordsAnalytics()
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/urls")
        {
            Content = JsonContent.Create(new CreateShortUrlRequest("https://example.com/docs", "integration-test"))
        };
        createRequest.Headers.Add("X-Correlation-ID", "integration-001");

        var createResponse = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("integration-001", createResponse.Headers.GetValues("X-Correlation-ID").Single());
        var created = await createResponse.Content.ReadFromJsonAsync<ShortUrlResponse>();
        Assert.NotNull(created);

        using var redirectRequest = new HttpRequestMessage(HttpMethod.Get, $"/{created!.Code}");
        redirectRequest.Headers.Referrer = new Uri("https://referrer.example");
        var redirectResponse = await client.SendAsync(redirectRequest);
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);
        Assert.Equal("https://example.com/docs", redirectResponse.Headers.Location!.ToString());

        var analytics = await client.GetFromJsonAsync<AnalyticsResponse>($"/api/urls/{created.Code}/analytics");
        Assert.NotNull(analytics);
        Assert.Equal(1, analytics!.TotalClicks);
        Assert.Equal("https://referrer.example/", analytics.RecentClicks.Single().Referrer);
    }

    [Fact]
    public async Task OrchestrationEndpointsExposeApprovalGates()
    {
        var startResponse = await client.PostAsJsonAsync("/api/orchestration/runs",
            new StartWorkflowRequest("Build and validate a URL shortener", "greenfield"));
        Assert.Equal(HttpStatusCode.Accepted, startResponse.StatusCode);
        var startBody = await startResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"AwaitingApproval\"", startBody);
        var run = await startResponse.Content.ReadFromJsonAsync<WorkflowRun>();
        Assert.NotNull(run);
        Assert.Equal(WorkflowStatus.AwaitingApproval, run!.Status);

        var approvalResponse = await client.PostAsJsonAsync($"/api/orchestration/runs/{run.Id}/approvals",
            new ApprovalRequest("Architecture", true, "integration-reviewer", "Architecture approved"));
        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);
        var approvedBody = await approvalResponse.Content.ReadAsStringAsync();
        var approvedRun = System.Text.Json.JsonSerializer.Deserialize<WorkflowRun>(approvedBody);
        Assert.NotNull(approvedRun);
        Assert.Contains("security-review", approvedBody);
    }
}