using DigitalTriage.Domain.Entities;

namespace DigitalTriage.Application.Contracts.Services;

/// <summary>
/// Provides statistics and analytics for patient issues with filtering capabilities.
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Gets available filter options (locations, hospitals) for the specified doctor.
    /// </summary>
    Task<StatisticsFiltersDto> GetFiltersAsync(int doctorId);

    /// <summary>
    /// Gets aggregated statistics based on the provided filters.
    /// </summary>
    Task<StatisticsSummaryDto> GetSummaryAsync(StatisticsQueryDto query, int doctorId);

    /// <summary>
    /// Exports statistics data in the specified format.
    /// </summary>
    Task<StatisticsExportDto> ExportAsync(StatisticsQueryDto query, string format, int doctorId);
}

/// <summary>
/// DTO for statistics filter options.
/// </summary>
public class StatisticsFiltersDto
{
    public List<LocationOptionDto> Countries { get; set; } = new();
    public List<LocationOptionDto> Counties { get; set; } = new();
    public List<LocationOptionDto> Cities { get; set; } = new();
    public List<HospitalOptionDto> Hospitals { get; set; } = new();
    public List<string> ProblemTypes { get; set; } = new();
}

/// <summary>
/// DTO for location options (country, county, city).
/// </summary>
public sealed record LocationOptionDto(int Id, string Name);

/// <summary>
/// DTO for hospital options.
/// </summary>
public sealed record HospitalOptionDto(int Id, string Name, string? City, string? County, string? Country);

/// <summary>
/// DTO for statistics query parameters.
/// </summary>
public sealed record StatisticsQueryDto
{
    public string? Level { get; init; } // "country", "county", "city"
    public int? LocationId { get; init; }
    public int? HospitalId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public List<string>? ProblemTypes { get; init; }
    public List<EsiLevel>? EmergencyGrades { get; init; }
}

/// <summary>
/// DTO for statistics summary response.
/// </summary>
public class StatisticsSummaryDto
{
    public int TotalActiveIssues { get; set; }
    public int UniquePatientsWithActiveIssues { get; set; }
    public int HospitalsIncluded { get; set; }
    public Dictionary<string, int> DistributionBySeverity { get; set; } = new();
    public Dictionary<string, int> DistributionByType { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> TypeSeverityMatrix { get; set; } = new();
    public List<TopHospitalDto> TopHospitals { get; set; } = new();
}

/// <summary>
/// DTO for top hospital statistics.
/// </summary>
public sealed record TopHospitalDto(int Id, string Name, int ActiveIssuesCount, int UniquePatientsCount);

/// <summary>
/// DTO for statistics export response.
/// </summary>
public sealed record StatisticsExportDto
{
    public required string Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}

