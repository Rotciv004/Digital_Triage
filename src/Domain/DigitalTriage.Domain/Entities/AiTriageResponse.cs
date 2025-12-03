using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

public class AiTriageResponse
{
    public int Id { get; set; }
    
    [Required]
    public int TriageSessionId { get; set; }
    
    [Required]
    public string Response { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Analysis { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation property
    public TriageSession? TriageSession { get; set; }
}