using DigitalTriage.Application.Contracts.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DigitalTriage.Infrastructure.Services;

/// <summary>
/// Service for sending emails via Microsoft Graph API (Outlook).
/// </summary>
internal sealed class EmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(
        IHttpClientFactory httpClientFactory,
        ILogger<EmailService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> SendFamilyMedicRequestNotificationAsync(
        string doctorEmail,
        string patientName,
        string patientEmail)
    {
        // This will be handled in-app, but we can also send an email if needed
        // For now, we'll just log it since the doctor will see it in-app
        _logger.LogInformation(
            "Family medic request notification: Patient {PatientName} ({PatientEmail}) requested doctor {DoctorEmail}",
            patientName,
            patientEmail,
            doctorEmail);

        // In a real implementation, you could send an email here using a system account
        // For now, we rely on in-app notifications
        return true;
    }

    public async Task<bool> SendProblemToFamilyMedicAsync(
        string patientEmail,
        string doctorEmail,
        string accessToken,
        string issueTitle,
        string issueDescription,
        string? problemType = null,
        int? emergencyGrade = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var emailBody = BuildEmailBody(issueTitle, issueDescription, problemType, emergencyGrade);

            var message = new
            {
                message = new
                {
                    subject = $"Medical Issue from Patient: {issueTitle}",
                    body = new
                    {
                        contentType = "HTML",
                        content = emailBody
                    },
                    toRecipients = new[]
                    {
                        new
                        {
                            emailAddress = new
                            {
                                address = doctorEmail
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                "https://graph.microsoft.com/v1.0/me/sendMail",
                content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Email sent successfully from {PatientEmail} to {DoctorEmail}",
                    patientEmail,
                    doctorEmail);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to send email. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending email from {PatientEmail} to {DoctorEmail}", patientEmail, doctorEmail);
            return false;
        }
    }

    private string BuildEmailBody(
        string issueTitle,
        string issueDescription,
        string? problemType,
        int? emergencyGrade)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head><style>body { font-family: Arial, sans-serif; }</style></head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<h2>Medical Issue Report</h2>");
        sb.AppendLine($"<p><strong>Title:</strong> {EscapeHtml(issueTitle)}</p>");
        
        if (!string.IsNullOrWhiteSpace(problemType))
        {
            sb.AppendLine($"<p><strong>Problem Type:</strong> {EscapeHtml(problemType)}</p>");
        }

        if (emergencyGrade.HasValue)
        {
            sb.AppendLine($"<p><strong>Emergency Grade:</strong> {emergencyGrade.Value}</p>");
        }

        sb.AppendLine($"<p><strong>Description:</strong></p>");
        sb.AppendLine($"<p>{EscapeHtml(issueDescription)}</p>");
        sb.AppendLine("<hr>");
        sb.AppendLine("<p><em>This message was sent from the Digital Triage application.</em></p>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}

