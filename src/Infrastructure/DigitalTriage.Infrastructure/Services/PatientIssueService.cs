using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalTriage.Infrastructure.Services;

/// <summary>
/// Provides CRUD operations for patient issues.
/// </summary>
internal sealed class PatientIssueService : IPatientIssueService
{
    private readonly MedicalTriageDbContext _dbContext;

    public PatientIssueService(MedicalTriageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PatientIssue>> GetByPatientIdAsync(int patientId)
    {
        var issues = await _dbContext.PatientIssues
            .Where(i => i.PatientId == patientId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return issues;
    }

    public async Task<PatientIssue> CreateAsync(int patientId, string title, string description, string? problemType = null, int? emergencyGrade = null)
    {
        var issue = new PatientIssue
        {
            PatientId = patientId,
            Title = title,
            Description = description,
            ProblemType = problemType,
            EmergencyGrade = emergencyGrade.HasValue ? (EsiLevel?)emergencyGrade.Value : null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdateDate = DateTimeOffset.UtcNow
        };

        _dbContext.PatientIssues.Add(issue);
        await _dbContext.SaveChangesAsync();
        return issue;
    }

    public async Task<PatientIssue> UpdateAsync(int issueId, string? problemType = null, EsiLevel? emergencyGrade = null, bool? isActive = null)
    {
        var issue = await _dbContext.PatientIssues
            .FirstOrDefaultAsync(i => i.Id == issueId)
            ?? throw new InvalidOperationException("Patient issue not found.");

        if (problemType != null)
        {
            issue.ProblemType = problemType;
        }

        if (emergencyGrade.HasValue)
        {
            issue.EmergencyGrade = emergencyGrade;
        }

        if (isActive.HasValue)
        {
            issue.IsActive = isActive.Value;
        }

        issue.LastUpdateDate = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        return issue;
    }

    public async Task<bool> DeleteAsync(int issueId, int patientId)
    {
        var issue = await _dbContext.PatientIssues
            .FirstOrDefaultAsync(i => i.Id == issueId && i.PatientId == patientId);

        if (issue == null)
        {
            return false;
        }

        _dbContext.PatientIssues.Remove(issue);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public Task<PatientIssue> CreateAsync(int patientId, string title, string description)
    {
        throw new NotImplementedException();
    }
}

