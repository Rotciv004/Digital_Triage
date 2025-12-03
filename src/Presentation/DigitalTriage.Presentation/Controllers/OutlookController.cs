using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Presentation.Common.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace DigitalTriage.Presentation.Controllers;

/// <summary>
/// Controller for handling Outlook OAuth2 authentication and token storage.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OutlookController : ControllerBase
{
    private readonly IFamilyMedicService _familyMedicService;
    private readonly IAuthHelper _authHelper;
    private readonly ILogger<OutlookController> _logger;
    private readonly IConfiguration _configuration;

    public OutlookController(
        IFamilyMedicService familyMedicService,
        IAuthHelper authHelper,
        ILogger<OutlookController> logger,
        IConfiguration configuration)
    {
        _familyMedicService = familyMedicService;
        _authHelper = authHelper;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Initiates the Outlook OAuth2 authentication flow.
    /// </summary>
    [HttpGet("connect")]
    public IActionResult Connect()
    {
        // Check if Microsoft authentication is configured
        var clientId = _configuration["MicrosoftGraph:ClientId"];
        var clientSecret = _configuration["MicrosoftGraph:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogWarning("Outlook OAuth connection attempted but Microsoft authentication is not configured");
            return Redirect("/medical-info?outlookError=not_configured");
        }

        // Store the return URL in a cookie or session to redirect after OAuth
        var properties = new AuthenticationProperties 
        { 
            RedirectUri = "/api/outlook/callback"
        };
        return Challenge(properties, "Microsoft");
    }

    /// <summary>
    /// Handles the OAuth callback and stores the refresh token.
    /// This endpoint is called by Microsoft after the user authenticates.
    /// </summary>
    [HttpGet("callback")]
    [Authorize]
    public async Task<IActionResult> Callback()
    {
        try
        {
            var userId = _authHelper.GetUserId(User);
            if (!userId.HasValue)
            {
                return Redirect("/medical-info?outlookError=not_authenticated");
            }

            // Get tokens from the authentication result
            var authenticateResult = await HttpContext.AuthenticateAsync("Microsoft");
            if (authenticateResult?.Succeeded != true)
            {
                _logger.LogWarning("Outlook OAuth authentication failed for user {UserId}", userId);
                return Redirect("/medical-info?outlookError=auth_failed");
            }

            var tokens = authenticateResult.Properties?.GetTokens();
            var refreshToken = tokens?.FirstOrDefault(t => t.Name == "refresh_token")?.Value;

            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("No refresh token received for user {UserId}", userId);
                return Redirect("/medical-info?outlookError=no_token");
            }

            // Get access token expiration
            var expiresIn = tokens?.FirstOrDefault(t => t.Name == "expires_at")?.Value;
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(90); // Default to 90 days if not specified
            if (!string.IsNullOrEmpty(expiresIn) && DateTimeOffset.TryParse(expiresIn, out var parsed))
            {
                expiresAt = parsed;
            }

            // Encrypt and store the refresh token
            var success = await _familyMedicService.StoreOutlookTokenAsync(userId.Value, refreshToken, expiresAt);

            if (success)
            {
                _logger.LogInformation("Outlook token stored successfully for user {UserId}", userId);
                return Redirect("/medical-info?outlookConnected=true");
            }
            else
            {
                _logger.LogError("Failed to store Outlook token for user {UserId}", userId);
                return Redirect("/medical-info?outlookError=storage_failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Outlook OAuth callback");
            return Redirect("/medical-info?outlookError=callback_error");
        }
    }

    /// <summary>
    /// Disconnects the Outlook account (removes stored token).
    /// </summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        var userId = _authHelper.GetUserId(User);
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            // Remove the token by storing null
            await _familyMedicService.StoreOutlookTokenAsync(userId.Value, string.Empty, DateTimeOffset.UtcNow);
            return Ok(new { success = true, message = "Outlook account disconnected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting Outlook for user {UserId}", userId);
            return StatusCode(500, new { success = false, message = "Failed to disconnect Outlook account" });
        }
    }
}

