using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents a problem/issue reported by a patient.
/// </summary>
public class PatientIssue
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Clinical category/type of the problem (e.g., "Cardiac", "Respiratory", "Neurological").
    /// </summary>
    [MaxLength(100)]
    public string? ProblemType { get; set; }

    /// <summary>
    /// Emergency severity grade using ESI (Emergency Severity Index) level.
    /// </summary>
    public EsiLevel? EmergencyGrade { get; set; }

    /// <summary>
    /// Current status of the issue (Active or Resolved).
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last update timestamp for tracking changes.
    /// </summary>
    public DateTimeOffset LastUpdateDate { get; set; } = DateTimeOffset.UtcNow;
}
