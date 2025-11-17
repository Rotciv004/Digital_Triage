using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents a request from a patient to assign a doctor as their family medic.
/// </summary>
public class FamilyMedicRequest
{
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, EmailAddress, MaxLength(200)]
    public string DoctorEmail { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// The doctor profile ID (set when doctor accepts the request).
    /// </summary>
    public int? DoctorId { get; set; }
    public DoctorProfile? Doctor { get; set; }

    /// <summary>
    /// Encrypted refresh token for patient's Outlook account (for sending emails).
    /// </summary>
    public string? OutlookToken { get; set; }

    /// <summary>
    /// Token expiration date.
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }
}

