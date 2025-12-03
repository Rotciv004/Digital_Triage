using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    
    [Required]
    public int PatientId { get; set; }
    
    public int? TriageSessionId { get; set; }
    
    [Required, MaxLength(20)]
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public Patient? Patient { get; set; }
    public TriageSession? TriageSession { get; set; }
}