using DigitalTriage.Domain.Entities;

namespace DigitalTriage.Application.Contracts.Services;

/// <summary>
/// Defines operations to manage patient issues/problems.
/// </summary>
public interface IPatientIssueService
{
    Task<IReadOnlyList<PatientIssue>> GetByPatientIdAsync(int patientId);
    Task<PatientIssue> CreateAsync(
        int patientId, 
        string title, 
        string description, 
        string? problemType = null, 
        int? emergencyGrade = null);
    Task<PatientIssue> UpdateAsync(
        int issueId, 
        string? problemType = null, 
        EsiLevel? emergencyGrade = null, 
        bool? isActive = null);
    Task<bool> DeleteAsync(int issueId, int patientId);
}
