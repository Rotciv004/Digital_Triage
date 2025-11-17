using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTriage.Presentation.Controllers;

/// <summary>
/// Example API controller showing how to validate antiforgery tokens.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ExampleApiController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;

    public ExampleApiController(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    /// <summary>
    /// Example POST endpoint that validates antiforgery token via attribute.
    /// </summary>
    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateData([FromBody] UpdateRequest request)
    {
        return Ok(new { message = "Data updated successfully", data = request });
    }

    /// <summary>
    /// Example POST endpoint with manual token validation.
    /// </summary>
    [HttpPost("update-manual")]
    public async Task<IActionResult> UpdateDataManual([FromBody] UpdateRequest request)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Invalid antiforgery token", details = ex.Message });
        }

        return Ok(new { message = "Data updated successfully", data = request });
    }
}

public sealed record UpdateRequest(string? Name, string? Value);
