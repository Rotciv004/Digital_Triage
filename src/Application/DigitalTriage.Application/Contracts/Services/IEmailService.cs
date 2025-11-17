namespace DigitalTriage.Application.Contracts.Services;

/// <summary>
/// Service for sending emails via Outlook/Microsoft Graph API.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send a notification email to a doctor about a family medic request.
    /// </summary>
    Task<bool> SendFamilyMedicRequestNotificationAsync(string doctorEmail, string patientName, string patientEmail);

    /// <summary>
    /// Send a medical problem/issue to the family medic via Outlook.
    /// </summary>
    Task<bool> SendProblemToFamilyMedicAsync(
        string patientEmail,
        string doctorEmail,
        string accessToken,
        string issueTitle,
        string issueDescription,
        string? problemType = null,
        int? emergencyGrade = null);
}

