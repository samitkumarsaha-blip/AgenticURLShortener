using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Application;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/urls")]
public sealed class UrlsController(IShortUrlService service) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("url-creation")]
    public async Task<ActionResult<ShortUrlResponse>> Create(CreateShortUrlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { code = response.Code }, response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid URL request", Detail = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Title = "URL conflict", Detail = exception.Message });
        }
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<ShortUrlResponse>> Get(string code, CancellationToken cancellationToken)
    {
        var context = new ClickContext(Request.Headers.Referer.FirstOrDefault(), Request.Headers.UserAgent.FirstOrDefault(), HttpContext.Connection.RemoteIpAddress?.ToString());
        var shortUrl = await service.ResolveAsync(code, context, cancellationToken);
        return shortUrl is null ? NotFound() : Redirect(shortUrl.Destination.ToString());
    }

    [HttpGet("{code}/analytics")]
    public async Task<ActionResult<AnalyticsResponse>> Analytics(string code, CancellationToken cancellationToken)
    {
        var analytics = await service.GetAnalyticsAsync(code, cancellationToken);
        return analytics is null ? NotFound() : Ok(analytics);
    }
}