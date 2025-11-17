using DigitalTriage.Application.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTriage.Presentation.Controllers;

/// <summary>
/// API controller for account management operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly IPatientService _patientService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IPatientService patientService, ILogger<AccountController> logger)
    {
        _patientService = patientService;
        _logger = logger;
    }

    /// <summary>
    /// Deletes the currently authenticated user's account.
    /// </summary>
    [HttpDelete("delete-current")]
    public async Task<IActionResult> DeleteCurrentUser()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("Delete account request failed: no valid user id found in claims");
            return BadRequest(new { error = "No logged-in user found." });
        }

        try
        {
            await _patientService.DeleteAsync(userId);
            _logger.LogInformation("Account deleted successfully for user id {UserId}", userId);
            return Ok(new { message = "User deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user account for user id {UserId}", userId);
            return StatusCode(500, new { error = "Failed to delete user.", details = ex.Message });
        }
    }
}
