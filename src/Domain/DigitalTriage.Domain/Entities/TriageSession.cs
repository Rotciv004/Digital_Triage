using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents an AI triage chat session for a patient.
/// </summary>
public class TriageSession
{
    public int Id { get; set; }
    
    [Required]
    public int PatientId { get; set; }
    
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? EndedAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Triage results
    [MaxLength(20)]
    public string? Severity { get; set; } // "Low", "Moderate", "High", "Critical"
    
    [MaxLength(500)]
    public string? RecommendedAction { get; set; }
    
    public bool IsEmergency { get; set; }
    
    // Navigation properties
    public Patient Patient { get; set; } = null!;
    public ICollection<TriageMessage> Messages { get; set; } = new List<TriageMessage>();
    public ICollection<AiTriageResponse> AiTriageResponses { get; set; } = new List<AiTriageResponse>();
}