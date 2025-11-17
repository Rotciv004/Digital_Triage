using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace DigitalTriage.Infrastructure.Services;

/// <summary>
/// Provides statistics and analytics for patient issues with filtering capabilities.
/// </summary>
internal sealed class StatisticsService : IStatisticsService
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly ILogger<StatisticsService> _logger;

    public StatisticsService(MedicalTriageDbContext dbContext, ILogger<StatisticsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<StatisticsFiltersDto> GetFiltersAsync(int doctorId)
    {
        var doctorProfile = await _dbContext.DoctorProfiles
            .FirstOrDefaultAsync(d => d.UserId == doctorId)
            ?? throw new InvalidOperationException("Doctor profile not found.");

        var authorizedHospitalIds = await _dbContext.DoctorHospitalMemberships
            .Where(m => m.DoctorId == doctorProfile.Id && m.IsActive)
            .Select(m => m.HospitalId)
            .ToListAsync();

        var hospitals = await _dbContext.Hospitals
            .Where(h => authorizedHospitalIds.Contains(h.Id))
            .ToListAsync();

        var countries = hospitals
            .Where(h => !string.IsNullOrWhiteSpace(h.Country))
            .Select(h => h.Country!)
            .Distinct()
            .OrderBy(c => c)
            .Select((c, idx) => new LocationOptionDto(idx + 1, c))
            .ToList();

        var counties = hospitals
            .Where(h => !string.IsNullOrWhiteSpace(h.County))
            .Select(h => h.County!)
            .Distinct()
            .OrderBy(c => c)
            .Select((c, idx) => new LocationOptionDto(idx + 1, c))
            .ToList();

        var cities = hospitals
            .Where(h => !string.IsNullOrWhiteSpace(h.City))
            .Select(h => h.City!)
            .Distinct()
            .OrderBy(c => c)
            .Select((c, idx) => new LocationOptionDto(idx + 1, c))
            .ToList();

        var hospitalOptions = hospitals
            .Select(h => new HospitalOptionDto(h.Id, h.Name, h.City, h.County, h.Country))
            .OrderBy(h => h.Name)
            .ToList();

        var problemTypes = await _dbContext.PatientIssues
            .Where(i => i.IsActive && i.ProblemType != null)
            .Select(i => i.ProblemType!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

        return new StatisticsFiltersDto
        {
            Countries = countries,
            Counties = counties,
            Cities = cities,
            Hospitals = hospitalOptions,
            ProblemTypes = problemTypes
        };
    }

    public async Task<StatisticsSummaryDto> GetSummaryAsync(StatisticsQueryDto query, int doctorId)
    {
        var doctorProfile = await _dbContext.DoctorProfiles
            .FirstOrDefaultAsync(d => d.UserId == doctorId)
            ?? throw new InvalidOperationException("Doctor profile not found.");

        var authorizedHospitalIds = await _dbContext.DoctorHospitalMemberships
            .Where(m => m.DoctorId == doctorProfile.Id && m.IsActive)
            .Select(m => m.HospitalId)
            .ToListAsync();

        var baseQuery = _dbContext.PatientIssues
            .Include(i => i.Patient)
                .ThenInclude(p => p!.PreferredHospital)
            .Where(i => i.IsActive && i.Patient != null && i.Patient.PreferredHospitalId.HasValue
                && authorizedHospitalIds.Contains(i.Patient.PreferredHospitalId.Value));

        // Apply location filter
        if (!string.IsNullOrWhiteSpace(query.Level) && query.LocationId.HasValue)
        {
            var locationValue = await GetLocationValueAsync(query.Level, query.LocationId.Value, authorizedHospitalIds);
            if (!string.IsNullOrWhiteSpace(locationValue))
            {
                baseQuery = query.Level.ToLowerInvariant() switch
                {
                    "country" => baseQuery.Where(i => i.Patient!.PreferredHospital!.Country == locationValue),
                    "county" => baseQuery.Where(i => i.Patient!.PreferredHospital!.County == locationValue),
                    "city" => baseQuery.Where(i => i.Patient!.PreferredHospital!.City == locationValue),
                    _ => baseQuery
                };
            }
        }

        // Apply hospital filter
        if (query.HospitalId.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.Patient!.PreferredHospitalId == query.HospitalId.Value);
        }

        // Apply date range filter
        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.LastUpdateDate >= query.FromDate.Value);
        }
        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.LastUpdateDate <= query.ToDate.Value);
        }

        // Apply problem type filter
        if (query.ProblemTypes != null && query.ProblemTypes.Any())
        {
            baseQuery = baseQuery.Where(i => i.ProblemType != null && query.ProblemTypes.Contains(i.ProblemType));
        }

        // Apply emergency grade filter
        if (query.EmergencyGrades != null && query.EmergencyGrades.Any())
        {
            baseQuery = baseQuery.Where(i => i.EmergencyGrade.HasValue && query.EmergencyGrades.Contains(i.EmergencyGrade.Value));
        }

        var issues = await baseQuery.ToListAsync();

        var totalActiveIssues = issues.Count;
        var uniquePatients = issues.Select(i => i.PatientId).Distinct().Count();
        var hospitalsIncluded = issues
            .Where(i => i.Patient?.PreferredHospitalId.HasValue == true)
            .Select(i => i.Patient!.PreferredHospitalId!.Value)
            .Distinct()
            .Count();

        // Distribution by severity (map EsiLevel to severity categories)
        var distributionBySeverity = new Dictionary<string, int>
        {
            ["Low"] = 0,
            ["Moderate"] = 0,
            ["High"] = 0,
            ["Critical"] = 0
        };

        foreach (var issue in issues.Where(i => i.EmergencyGrade.HasValue))
        {
            var severity = MapEsiLevelToSeverity(issue.EmergencyGrade!.Value);
            distributionBySeverity[severity]++;
        }

        // Distribution by type
        var distributionByType = issues
            .Where(i => !string.IsNullOrWhiteSpace(i.ProblemType))
            .GroupBy(i => i.ProblemType!)
            .ToDictionary(g => g.Key, g => g.Count());

        // Type × Severity matrix
        var typeSeverityMatrix = new Dictionary<string, Dictionary<string, int>>();
        foreach (var issue in issues.Where(i => !string.IsNullOrWhiteSpace(i.ProblemType) && i.EmergencyGrade.HasValue))
        {
            var type = issue.ProblemType!;
            var severity = MapEsiLevelToSeverity(issue.EmergencyGrade!.Value);

            if (!typeSeverityMatrix.ContainsKey(type))
            {
                typeSeverityMatrix[type] = new Dictionary<string, int>
                {
                    ["Low"] = 0,
                    ["Moderate"] = 0,
                    ["High"] = 0,
                    ["Critical"] = 0
                };
            }

            typeSeverityMatrix[type][severity]++;
        }

        // Top hospitals (if no hospital filter applied)
        var topHospitals = new List<TopHospitalDto>();
        if (!query.HospitalId.HasValue)
        {
            topHospitals = issues
                .Where(i => i.Patient?.PreferredHospital != null)
                .GroupBy(i => new { i.Patient!.PreferredHospital!.Id, i.Patient.PreferredHospital.Name })
                .Select(g => new TopHospitalDto(
                    g.Key.Id,
                    g.Key.Name,
                    g.Count(),
                    g.Select(i => i.PatientId).Distinct().Count()
                ))
                .OrderByDescending(h => h.ActiveIssuesCount)
                .Take(10)
                .ToList();
        }

        return new StatisticsSummaryDto
        {
            TotalActiveIssues = totalActiveIssues,
            UniquePatientsWithActiveIssues = uniquePatients,
            HospitalsIncluded = hospitalsIncluded,
            DistributionBySeverity = distributionBySeverity,
            DistributionByType = distributionByType,
            TypeSeverityMatrix = typeSeverityMatrix,
            TopHospitals = topHospitals
        };
    }

    public async Task<StatisticsExportDto> ExportAsync(StatisticsQueryDto query, string format, int doctorId)
    {
        var summary = await GetSummaryAsync(query, doctorId);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            return new StatisticsExportDto
            {
                Content = json,
                ContentType = "application/json",
                FileName = $"statistics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json"
            };
        }
        else // CSV
        {
            var csv = new StringBuilder();
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Active Issues,{summary.TotalActiveIssues}");
            csv.AppendLine($"Unique Patients,{summary.UniquePatientsWithActiveIssues}");
            csv.AppendLine($"Hospitals Included,{summary.HospitalsIncluded}");
            csv.AppendLine();
            csv.AppendLine("Severity Distribution");
            foreach (var kvp in summary.DistributionBySeverity)
            {
                csv.AppendLine($"{kvp.Key},{kvp.Value}");
            }
            csv.AppendLine();
            csv.AppendLine("Type Distribution");
            foreach (var kvp in summary.DistributionByType)
            {
                csv.AppendLine($"{kvp.Key},{kvp.Value}");
            }
            csv.AppendLine();
            csv.AppendLine("Type × Severity Matrix");
            csv.AppendLine("Type,Low,Moderate,High,Critical");
            foreach (var type in summary.TypeSeverityMatrix)
            {
                var low = type.Value.TryGetValue("Low", out var lowVal) ? lowVal : 0;
                var moderate = type.Value.TryGetValue("Moderate", out var modVal) ? modVal : 0;
                var high = type.Value.TryGetValue("High", out var highVal) ? highVal : 0;
                var critical = type.Value.TryGetValue("Critical", out var critVal) ? critVal : 0;
                var row = $"{type.Key},{low},{moderate},{high},{critical}";
                csv.AppendLine(row);
            }

            return new StatisticsExportDto
            {
                Content = csv.ToString(),
                ContentType = "text/csv",
                FileName = $"statistics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"
            };
        }
    }

    private async Task<string?> GetLocationValueAsync(string level, int locationId, List<int> authorizedHospitalIds)
    {
        var hospitals = await _dbContext.Hospitals
            .Where(h => authorizedHospitalIds.Contains(h.Id))
            .ToListAsync();

        return level.ToLowerInvariant() switch
        {
            "country" => hospitals
                .Where(h => !string.IsNullOrWhiteSpace(h.Country))
                .Select(h => h.Country!)
                .Distinct()
                .OrderBy(c => c)
                .ElementAtOrDefault(locationId - 1),
            "county" => hospitals
                .Where(h => !string.IsNullOrWhiteSpace(h.County))
                .Select(h => h.County!)
                .Distinct()
                .OrderBy(c => c)
                .ElementAtOrDefault(locationId - 1),
            "city" => hospitals
                .Where(h => !string.IsNullOrWhiteSpace(h.City))
                .Select(h => h.City!)
                .Distinct()
                .OrderBy(c => c)
                .ElementAtOrDefault(locationId - 1),
            _ => null
        };
    }

    private static string MapEsiLevelToSeverity(EsiLevel esiLevel)
    {
        return esiLevel switch
        {
            EsiLevel.Resuscitation => "Critical",
            EsiLevel.Critical => "Critical",
            EsiLevel.Urgent => "High",
            EsiLevel.NonUrgent => "Moderate",
            EsiLevel.Consult => "Low",
            _ => "Moderate"
        };
    }
}

