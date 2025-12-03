namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents a message in an AI triage session.
/// </summary>
public class TriageMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    
    // Navigation property
    public TriageSession Session { get; set; } = null!;
}