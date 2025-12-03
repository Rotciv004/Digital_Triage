using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Stores doctor-specific data that extends the base patient user account.
/// </summary>
public class DoctorProfile
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public Patient User { get; set; } = default!;

    [Required, MaxLength(150)]
    public string Specialization { get; set; } = string.Empty;

    public ICollection<DoctorHospitalMembership> HospitalMemberships { get; set; } = new List<DoctorHospitalMembership>();
    public ICollection<Hospital> CreatedHospitals { get; set; } = new List<Hospital>();
    public ICollection<MedicalData> AuthorizedMedicalData { get; set; } = new List<MedicalData>();
    public ICollection<Patient> FamilyPatients { get; set; } = new List<Patient>();
    public ICollection<FamilyMedicRequest> FamilyMedicRequests { get; set; } = new List<FamilyMedicRequest>();
}
