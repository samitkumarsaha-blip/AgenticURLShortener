using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Application;
using UrlShortener.Api.Telemetry;
using UrlShortener.Infrastructure;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<RequestMetrics>();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("url-creation", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "Rate limit exceeded",
            Detail = "Try again later."
        }, cancellationToken);
    };
});
builder.Services.AddDbContextFactory<ShortUrlDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Shortener") ?? "Data Source=urlshortener.db"));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<IShortUrlRepository, ShortUrlRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IShortUrlService, ShortUrlService>();
builder.Services.AddSingleton<IWorkflowRunStore, WorkflowRunStore>();
builder.Services.AddSingleton<IWorkflowOrchestrator, WorkflowOrchestrator>();
builder.Services.AddSingleton<IWorkflowAgent, RequirementsAgent>();
builder.Services.AddSingleton<IWorkflowAgent, ArchitectureAgent>();
builder.Services.AddSingleton<IWorkflowAgent, ImplementationAgent>();
builder.Services.AddSingleton<IWorkflowAgent, QaValidationAgent>();
builder.Services.AddSingleton<IWorkflowAgent, ObservabilityAgent>();
builder.Services.AddSingleton<IWorkflowAgent, ReleaseReadinessAgent>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ShortUrlDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/index.html", () => Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html"));
app.MapGet("/report.html", () => Results.File(Path.Combine(app.Environment.WebRootPath, "report.html"), "text/html"));

app.MapControllers();

app.Run();

public partial class Program;
