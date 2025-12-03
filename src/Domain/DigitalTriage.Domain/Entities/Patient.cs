using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents a patient (or doctor/admin) account.
/// </summary>
public class Patient
{
    public int Id { get; set; }

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(13)]
    public string? Cnp { get; set; }

    [MaxLength(2)]
    public string? Serie { get; set; }

    [MaxLength(6)]
    public string? Nr { get; set; }

    [MaxLength(100)]
    public string? Citizenship { get; set; }

    public int? PlaceOfBirthId { get; set; }
    public PlaceOfBirth? PlaceOfBirth { get; set; }

    public int? DomicileId { get; set; }
    public Domicile? Domicile { get; set; }

    [MaxLength(20)]
    public string? Role { get; set; }

    public int? PreferredHospitalId { get; set; }
    public Hospital? PreferredHospital { get; set; }

    public DoctorProfile? DoctorProfile { get; set; }

    /// <summary>
    /// Email address of the assigned family medic.
    /// </summary>
    [EmailAddress, MaxLength(200)]
    public string? FamilyMedicEmail { get; set; }

    /// <summary>
    /// The doctor profile ID of the assigned family medic.
    /// </summary>
    public int? FamilyMedicDoctorId { get; set; }
    public DoctorProfile? FamilyMedicDoctor { get; set; }

    public ICollection<MedicalData> MedicalDatas { get; set; } = new List<MedicalData>();
    public ICollection<PatientIssue> Issues { get; set; } = new List<PatientIssue>();
    public ICollection<FamilyMedicRequest> FamilyMedicRequests { get; set; } = new List<FamilyMedicRequest>();
}
