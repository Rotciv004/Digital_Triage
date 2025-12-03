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
 private readonly string? _apiKey;
    private readonly string _modelName;

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
        _apiKey = config["Gemini:ApiKey"];
        _modelName = config["Gemini:Model"] ?? "gemini-1.5-flash";
        
        _logger.LogInformation("AiTriageService initialized with model: {Model}, API Key configured: {HasKey}", 
  _modelName, !string.IsNullOrEmpty(_apiKey));
    }

    public async Task<TriageSessionDto> StartSessionAsync(int patientId)
    {
        // End any existing active sessions
        var activeSessions = await _context.TriageSessions
   .Where(s => s.PatientId == patientId && s.IsActive)
.ToListAsync();
        
        foreach (var session in activeSessions)
        {
         session.IsActive = false;
            session.EndedAt = DateTimeOffset.UtcNow;
        }

        // Create new session
        var newSession = new TriageSession
        {
  PatientId = patientId,
      StartedAt = DateTimeOffset.UtcNow,
         IsActive = true
        };

        _context.TriageSessions.Add(newSession);
        
        // Add initial greeting message
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

        // Save user message
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
   if (string.IsNullOrEmpty(_apiKey))
    {
       _logger.LogWarning("Gemini API key is not configured");
            return new AiResponse
    {
     Content = "⚠️ AI service is not configured. Please add your Gemini API key to appsettings.json.",
             Severity = null,
             IsEmergency = false,
  RecommendedAction = null
            };
        }

        try
        {
        _logger.LogInformation("Calling Gemini API directly with model: {Model}", _modelName);
            
       var httpClient = _httpClientFactory.CreateClient();
     httpClient.Timeout = TimeSpan.FromSeconds(30);
            
      // Build context with medical history
 var context = BuildMedicalContext(session.Patient);
            
    // Build simple prompt
       var fullPrompt = $@"{context}

Patient's current message: {userMessage}

Based on the symptoms described, provide:
1. A helpful, empathetic response (2-3 sentences)
2. Assessment of severity (Low/Moderate/High/Critical)
3. Whether this is an emergency (Yes/No)
4. Recommended next steps (1-2 sentences)

Format your response naturally, but clearly state the severity level.
Remember: You are providing preliminary triage, not diagnosis.";

            // Prepare request for Gemini API v1beta
          var requestBody = new
     {
          contents = new[]
        {
     new
        {
          parts = new[]
      {
       new { text = fullPrompt }
        }
         }
     }
            };

  // ✅ Back to v1beta with compatible model
   var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
     
            _logger.LogInformation("Sending request to Gemini API v1beta...");
            _logger.LogDebug("API URL (masked): https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key=***", _modelName);
  
  var jsonContent = JsonSerializer.Serialize(requestBody);
            _logger.LogDebug("Request body length: {Length} characters", jsonContent.Length);
         
  var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
    var response = await httpClient.PostAsync(apiUrl, content);
            
      var responseContent = await response.Content.ReadAsStringAsync();
_logger.LogDebug("Response status: {StatusCode}", response.StatusCode);
            _logger.LogDebug("Response content length: {Length}", responseContent?.Length ?? 0);
            
   if (!response.IsSuccessStatusCode)
          {
   _logger.LogError("Gemini API error: {StatusCode} - {Error}", response.StatusCode, responseContent);
          
    return new AiResponse
    {
      Content = $"I'm having trouble connecting to the AI service. Status: {response.StatusCode}. Please try again in a moment.",
    Severity = "Moderate",
      IsEmergency = false,
   RecommendedAction = "If symptoms are severe, seek medical attention immediately."
    };
     }

      var jsonResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, new JsonSerializerOptions 
            { 
              PropertyNameCaseInsensitive = true 
 });
    
var responseText = jsonResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            
            if (string.IsNullOrEmpty(responseText))
     {
          _logger.LogWarning("Received empty response from Gemini API");
     _logger.LogDebug("Full API response: {Response}", responseContent);
   
        return new AiResponse
      {
         Content = "I received an empty response from the AI service. Please try rephrasing your question.",
             Severity = "Moderate",
          IsEmergency = false,
     RecommendedAction = "Please try again or seek medical advice if symptoms persist."
           };
            }
    
            _logger.LogInformation("Successfully received response from Gemini: {Length} chars", responseText.Length);

         // Parse response for severity and emergency status
         var (severity, isEmergency) = ParseSeverity(responseText);

            return new AiResponse
            {
         Content = responseText,
        Severity = severity,
     IsEmergency = isEmergency,
       RecommendedAction = ExtractRecommendation(responseText)
  };
        }
 catch (TaskCanceledException ex)
        {
     _logger.LogError(ex, "Gemini API call timed out after 30 seconds");
 return new AiResponse
       {
            Content = "The AI service is taking too long to respond. Please try again with a shorter message.",
 Severity = "Moderate",
 IsEmergency = false,
            RecommendedAction = "Please try again or seek medical advice if urgent."
    };
        }
   catch (HttpRequestException ex)
        {
       _logger.LogError(ex, "Network error calling Gemini API: {Message}", ex.Message);
            return new AiResponse
   {
       Content = $"Unable to reach the AI service. Network error: {ex.Message}. Please check your internet connection.",
     Severity = null,
      IsEmergency = false,
      RecommendedAction = null
  };
        }
      catch (JsonException ex)
        {
    _logger.LogError(ex, "Error parsing Gemini API response: {Message}", ex.Message);
       return new AiResponse
            {
             Content = "Received an invalid response from the AI service. Please try again.",
              Severity = null,
    IsEmergency = false,
        RecommendedAction = null
         };
    }
        catch (Exception ex)
        {
   _logger.LogError(ex, "Unexpected error calling Gemini API: {Message}", ex.Message);
        _logger.LogDebug("Exception type: {Type}, Stack trace: {StackTrace}", ex.GetType().Name, ex.StackTrace);
     
         return new AiResponse
            {
             Content = $"An unexpected error occurred: {ex.Message}. Please try again in a moment, or seek immediate medical attention if this is urgent.",
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

    // Gemini API response models
    private class GeminiResponse
    {
      [JsonPropertyName("candidates")]
   public List<Candidate>? Candidates { get; set; }
    }

    private class Candidate
    {
    [JsonPropertyName("content")]
      public ContentPart? Content { get; set; }
    }

    private class ContentPart
    {
     [JsonPropertyName("parts")]
        public List<TextPart>? Parts { get; set; }
    }

    private class TextPart
    {
        [JsonPropertyName("text")]
 public string? Text { get; set; }
    }

    private class AiResponse
    {
        public string Content { get; set; } = string.Empty;
  public string? Severity { get; set; }
        public bool IsEmergency { get; set; }
   public string? RecommendedAction { get; set; }
    }
}