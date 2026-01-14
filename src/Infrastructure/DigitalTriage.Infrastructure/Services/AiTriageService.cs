using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalTriage.Infrastructure.Services;

public class AiTriageService : IAiTriageService
{
    private readonly MedicalTriageDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<AiTriageService> _logger;      
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _customAiEndpoint;

    public AiTriageService(
        MedicalTriageDbContext context,
        IConfiguration config,
        ILogger<AiTriageService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _customAiEndpoint = config["CustomAI:Endpoint"] ?? "http://localhost:8000/predict";
        
        _logger.LogInformation("AiTriageService initialized with endpoint: {Endpoint}", _customAiEndpoint);
    }

    public async Task<TriageSessionDto> StartSessionAsync(int patientId)
    {
        var activeSessions = await _context.TriageSessions
            .Where(s => s.PatientId == patientId && s.IsActive)
            .ToListAsync();
        
        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.EndedAt = DateTimeOffset.UtcNow;
        }

        var newSession = new TriageSession
        {
            PatientId = patientId,
            StartedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _context.TriageSessions.Add(newSession);
        
        var greeting = new TriageMessage
        {
            Session = newSession,
            Role = "model",
            Content = "Hello! I'm your AI Triage Assistant. I'll help assess your symptoms and provide preliminary recommendations. Please describe your symptoms in detail, including when they started and how severe they are.\n\n⚠️ **Important:** This is NOT a substitute for professional medical advice. For emergencies, call your local emergency number immediately.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.TriageMessages.Add(greeting);
        await _context.SaveChangesAsync();

        return MapToDto(newSession);
    }

    public async Task<TriageSessionDto?> GetActiveSessionAsync(int patientId)
    {
        var session = await _context.TriageSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.PatientId == patientId && s.IsActive);

        return session == null ? null : MapToDto(session);
    }

    public async Task<ChatResponseDto> SendMessageAsync(int sessionId, string message)
    {
        _logger.LogInformation("SendMessageAsync called for session {SessionId}", sessionId);
        
        var session = await _context.TriageSessions
            .Include(s => s.Messages)
            .Include(s => s.Patient)
            .ThenInclude(p => p.MedicalDatas)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.IsActive);

        if (session == null)
        {
            _logger.LogError("Session {SessionId} not found or inactive", sessionId);
            throw new InvalidOperationException("Session not found or inactive");
        }

        var userMsg = new TriageMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = message,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.TriageMessages.Add(userMsg);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User message saved. Calling AI...");

        // Get AI response with timeout
        var aiResponse = await GetAiResponseAsync(session, message);
        
        _logger.LogInformation("AI response received: {ContentLength} characters", aiResponse.Content.Length);

        // Save assistant message
        var assistantMsg = new TriageMessage
        {
            SessionId = sessionId,
            Role = "model",
            Content = aiResponse.Content,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.TriageMessages.Add(assistantMsg);

        // Update session with assessment
        session.Severity = aiResponse.Severity;
        session.IsEmergency = aiResponse.IsEmergency;
        session.RecommendedAction = aiResponse.RecommendedAction;

        await _context.SaveChangesAsync();

        return new ChatResponseDto
        {
            UserMessage = MapMessageToDto(userMsg),
            AssistantMessage = MapMessageToDto(assistantMsg),
            Severity = aiResponse.Severity,
            IsEmergency = aiResponse.IsEmergency
        };
    }

    public async Task<bool> EndSessionAsync(int sessionId)
    {
        var session = await _context.TriageSessions.FindAsync(sessionId);
        if (session == null) return false;

        session.IsActive = false;
        session.EndedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }   

    private async Task<AiResponse> GetAiResponseAsync(TriageSession session, string userMessage)
    {
        if (string.IsNullOrEmpty(_customAiEndpoint))
        {
            _logger.LogWarning("Personal AI endpoint is not configured");
            return new AiResponse
            {
   Content = "⚠️ AI service is not configured. Please check your endpoint URL.",
         Severity = null,
   IsEmergency = false,
  RecommendedAction = null
            };
        }

        try
 {
     _logger.LogInformation("Calling Personal AI at: {Endpoint}", _customAiEndpoint);
  
  var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
       
          // Build context with medical history
         var context = BuildMedicalContext(session.Patient);
   
  // Prepare request body for your custom AI (matching your endpoint format)
       var requestBody = new
            {
     complaint = userMessage,
     history = context
       };

            _logger.LogInformation("Sending request to Custom AI...");
   
    var jsonContent = JsonSerializer.Serialize(requestBody);
       _logger.LogDebug("Request body: {Body}", jsonContent);
        
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
  
  var response = await httpClient.PostAsync(_customAiEndpoint, content);
            
        var responseContent = await response.Content.ReadAsStringAsync();
     _logger.LogDebug("Response status: {StatusCode}", response.StatusCode);
  _logger.LogDebug("Response: {Response}", responseContent);
 
       if (!response.IsSuccessStatusCode)
         {
    _logger.LogError("Custom AI API error: {StatusCode} - {Error}", response.StatusCode, responseContent);
              
                return new AiResponse
              {
    Content = $"AI service error: {response.StatusCode}. Please ensure your AI server is running.",
Severity = "Moderate",
              IsEmergency = false,
          RecommendedAction = "If symptoms are severe, seek medical attention immediately."
  };
  }

            // Parse response from your Custom AI
    var jsonResponse = JsonSerializer.Deserialize<CustomAiResponse>(responseContent, new JsonSerializerOptions 
    { 
      PropertyNameCaseInsensitive = true 
            });
            
      if (string.IsNullOrEmpty(jsonResponse?.Prediction))
 {
  _logger.LogWarning("Received empty prediction from Custom AI");
      
          return new AiResponse
    {
      Content = "Empty response from AI. Please try again.",
   Severity = "Moderate",
        IsEmergency = false,
                    RecommendedAction = "Please try again or seek medical advice if symptoms persist."
   };
         }
            
            _logger.LogInformation("Successfully received response. Confidence: {Confidence}", jsonResponse.Confidence);

    // Build comprehensive response text
            var formattedResponse = $"{jsonResponse.Prediction}\n\nConfidence: {jsonResponse.Confidence:P0}";
       if (!string.IsNullOrEmpty(jsonResponse.DebugInfo))
    {
     formattedResponse += $"\n\n{jsonResponse.DebugInfo.Replace("|", "\n")}";
}

          // Parse severity and emergency status
       var (severity, isEmergency) = ParseSeverity(jsonResponse.Prediction);

            return new AiResponse
  {
         Content = formattedResponse,
                Severity = severity,
       IsEmergency = isEmergency,
    RecommendedAction = jsonResponse.DebugInfo
      };
        }
        catch (TaskCanceledException ex)
        {
  _logger.LogError(ex, "Custom AI API call timed out");
      return new AiResponse
  {
      Content = "AI service timeout. Please try again.",
           Severity = "Moderate",
              IsEmergency = false,
          RecommendedAction = "Please try again or seek medical advice if urgent."
            };
        }
        catch (HttpRequestException ex)
        {
        _logger.LogError(ex, "Network error calling Custom AI: {Message}", ex.Message);
     return new AiResponse
       {
     Content = $"Cannot reach AI server at {_customAiEndpoint}. Is it running?",
   Severity = null,
     IsEmergency = false,
   RecommendedAction = "Please ensure your AI server is running on port 8000."
  };
        }
        catch (Exception ex)
        {
        _logger.LogError(ex, "Error calling Custom AI: {Message}", ex.Message);
   
            return new AiResponse
     {
      Content = $"Error: {ex.Message}",
      Severity = null,
             IsEmergency = false,
          RecommendedAction = null
            };
        }
    }

    private static string BuildMedicalContext(Patient patient)
    {
        var medicalData = patient.MedicalDatas?.FirstOrDefault();
        if (medicalData == null)
            return "You are a medical triage AI assistant. Provide empathetic, evidence-based preliminary assessments.";

        return $@"You are a medical triage AI assistant. Here is the patient's medical history:

Blood Type: {medicalData.BloodType ?? "Unknown"}
Known Allergies: {medicalData.Allergies ?? "None reported"}
Chronic Conditions: {medicalData.ChronicDiseases ?? "None reported"}
Current Medications: {medicalData.CurrentMedication ?? "None reported"}

Provide empathetic, evidence-based preliminary assessments considering this history.
Always remind the patient this is not a diagnosis and to seek professional care.";
    }

    private static (string? severity, bool isEmergency) ParseSeverity(string response)
    {
        var lower = response.ToLowerInvariant();
        
        // Check for emergency keywords
        if (lower.Contains("emergency") || lower.Contains("911") || lower.Contains("call emergency") || 
            lower.Contains("seek immediate") || lower.Contains("life-threatening"))
            return ("Critical", true);
        
        // Check for severity levels
        if (lower.Contains("critical") || lower.Contains("severe"))
            return ("Critical", false);
        
        if (lower.Contains("high severity") || lower.Contains("urgent"))
            return ("High", false);
        
        if (lower.Contains("moderate"))
            return ("Moderate", false);
        
        if (lower.Contains("low") || lower.Contains("minor") || lower.Contains("mild"))
            return ("Low", false);

        // Default to moderate if unclear
        return ("Moderate", false);
    }

    private static string? ExtractRecommendation(string response)
    {
        var lines = response.Split('\n');
        var recommendLine = lines.FirstOrDefault(l => 
            l.ToLowerInvariant().Contains("recommend") || 
            l.ToLowerInvariant().Contains("next step"));
        
        return recommendLine?.Trim() ?? response.Substring(0, Math.Min(200, response.Length));
    }

    private static TriageSessionDto MapToDto(TriageSession session)
    {
        return new TriageSessionDto
        {
            Id = session.Id,
            PatientId = session.PatientId,
            StartedAt = session.StartedAt,
            IsActive = session.IsActive,
            Severity = session.Severity,
            RecommendedAction = session.RecommendedAction,
            IsEmergency = session.IsEmergency,
            Messages = session.Messages?.Select(MapMessageToDto).ToList() ?? new()
        };
    }

    private static ChatMessageDto MapMessageToDto(TriageMessage message)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            Role = message.Role == "model" ? "assistant" : message.Role,
            Content = message.Content,
            CreatedAt = message.CreatedAt
        };
    }

    // Custom AI response model (matching your API format)
    private class CustomAiResponse
    {
    [JsonPropertyName("prediction")]
        public string Prediction { get; set; } = string.Empty;

     [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("debug_info")]
        public string? DebugInfo { get; set; }
    }

    private class AiResponse
    {
   public string Content { get; set; } = string.Empty;
    public string? Severity { get; set; }
   public bool IsEmergency { get; set; }
        public string? RecommendedAction { get; set; }
    }
}