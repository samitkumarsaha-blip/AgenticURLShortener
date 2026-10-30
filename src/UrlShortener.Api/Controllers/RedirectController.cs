using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("")]
public sealed class RedirectController(IShortUrlService service) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Redirect(string code, CancellationToken cancellationToken)
    {
        var context = new ClickContext(
            Request.Headers.Referer.FirstOrDefault(),
            Request.Headers.UserAgent.FirstOrDefault(),
            HttpContext.Connection.RemoteIpAddress?.ToString());
        var shortUrl = await service.ResolveAsync(code, context, cancellationToken);
        return shortUrl is null ? NotFound() : base.Redirect(shortUrl.Destination.ToString());
    }
}