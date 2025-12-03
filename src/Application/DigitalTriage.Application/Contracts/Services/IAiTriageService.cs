namespace DigitalTriage.Application.Contracts.Services;

public interface IAiTriageService
{
    Task<TriageSessionDto> StartSessionAsync(int patientId);
    Task<TriageSessionDto?> GetActiveSessionAsync(int patientId);
    Task<ChatResponseDto> SendMessageAsync(int sessionId, string message);
    Task<bool> EndSessionAsync(int sessionId);
}

public class TriageSessionDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public bool IsActive { get; set; }
    public string? Severity { get; set; }
    public string? RecommendedAction { get; set; }
    public bool IsEmergency { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class ChatResponseDto
{
    public ChatMessageDto UserMessage { get; set; } = null!;
    public ChatMessageDto AssistantMessage { get; set; } = null!;
    public string? Severity { get; set; }
    public bool IsEmergency { get; set; }
}