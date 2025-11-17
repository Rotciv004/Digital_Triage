using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DigitalTriage.Presentation.Controllers;

/// <summary>
/// API controller for statistics and analytics endpoints.
/// </summary>
[ApiController]
[Route("api/stats")]
[Authorize(Roles = "Doctor")]
public sealed class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _statisticsService;
    private readonly ILogger<StatisticsController> _logger;

    public StatisticsController(IStatisticsService statisticsService, ILogger<StatisticsController> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets available filter options for the current doctor.
    /// </summary>
    [HttpGet("filters")]
    public async Task<IActionResult> GetFilters()
    {
        var doctorId = GetDoctorId();
        if (doctorId == null)
        {
            return Unauthorized(new { error = "Doctor ID not found in claims." });
        }

        try
        {
            var filters = await _statisticsService.GetFiltersAsync(doctorId.Value);
            return Ok(filters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statistics filters for doctor {DoctorId}", doctorId);
            return StatusCode(500, new { error = "Failed to retrieve filter options.", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets aggregated statistics based on query parameters.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] StatisticsQueryDto query)
    {
        var doctorId = GetDoctorId();
        if (doctorId == null)
        {
            return Unauthorized(new { error = "Doctor ID not found in claims." });
        }

        try
        {
            var summary = await _statisticsService.GetSummaryAsync(query, doctorId.Value);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statistics summary for doctor {DoctorId}", doctorId);
            return StatusCode(500, new { error = "Failed to retrieve statistics.", details = ex.Message });
        }
    }

    /// <summary>
    /// Exports statistics data in the specified format (CSV or JSON).
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] StatisticsQueryDto query, [FromQuery] string format = "csv")
    {
        var doctorId = GetDoctorId();
        if (doctorId == null)
        {
            return Unauthorized(new { error = "Doctor ID not found in claims." });
        }

        if (!format.Equals("csv", StringComparison.OrdinalIgnoreCase) && 
            !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Format must be 'csv' or 'json'." });
        }

        try
        {
            var export = await _statisticsService.ExportAsync(query, format, doctorId.Value);
            
            _logger.LogInformation("Doctor {DoctorId} exported statistics in {Format} format", doctorId, format);
            
            return File(
                System.Text.Encoding.UTF8.GetBytes(export.Content),
                export.ContentType,
                export.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting statistics for doctor {DoctorId}", doctorId);
            return StatusCode(500, new { error = "Failed to export statistics.", details = ex.Message });
        }
    }

    private int? GetDoctorId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }
        return userId;
    }
}

