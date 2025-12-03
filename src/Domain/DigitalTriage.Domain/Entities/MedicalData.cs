using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents medical data associated with a patient.
/// </summary>
public class MedicalData
{
    public int Id { get; set; }

    [MaxLength(5)]
    public string? BloodType { get; set; }

    [MaxLength(1000)]
    public string? Allergies { get; set; }

    [MaxLength(1000)]
    public string? ChronicDiseases { get; set; }

    [MaxLength(1000)]
    public string? CurrentMedication { get; set; }

    [MaxLength(1000)]
    public string? PersonalHistory { get; set; }

    [MaxLength(1000)]
    public string? FamilyHistory { get; set; }

    [MaxLength(500)]
    public string? LivingConditions { get; set; }

    [MaxLength(200)]
    public string? IncidentLocation { get; set; }

    [MaxLength(2000)]
    public string? Symptoms { get; set; }

    [MaxLength(2000)]
    public string? PreliminaryDiagnosis { get; set; }

    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    public string? EmergencyContactPhone { get; set; }

    public DateTime? LastVisitDate { get; set; }

    [MaxLength(100)]
    public string? TriageCategory { get; set; }

    public EsiLevel? TriageLevel { get; set; }

    public int? EstimatedWaitTimeMinutes { get; set; }

    public bool IsConfidential { get; set; } = true;

    public int? AuthorizedDoctorId { get; set; }
    public DoctorProfile? AuthorizedDoctor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public ICollection<MedicalFile> Files { get; set; } = new List<MedicalFile>();
}
