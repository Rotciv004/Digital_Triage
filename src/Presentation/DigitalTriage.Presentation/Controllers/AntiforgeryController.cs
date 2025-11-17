using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTriage.Presentation.Controllers;

/// <summary>
/// Controller to provide antiforgery tokens to JavaScript clients.
/// </summary>
[ApiController]
[Route("antiforgery")]
public sealed class AntiforgeryController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;

    public AntiforgeryController(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    /// <summary>
    /// Gets an antiforgery token for use in JavaScript requests.
    /// </summary>
    [HttpGet("token")]
    public IActionResult GetToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }
}

