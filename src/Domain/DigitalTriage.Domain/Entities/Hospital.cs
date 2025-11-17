using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents a medical facility that can be created and managed by doctors.
/// </summary>
public class Hospital
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(100)]
    public string? County { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(150)]
    public string? Street { get; set; }

    [MaxLength(20)]
    public string? Number { get; set; }

    public int? CreatedByDoctorId { get; set; }
    public DoctorProfile? CreatedByDoctor { get; set; }

    public ICollection<DoctorHospitalMembership> DoctorMemberships { get; set; } = new List<DoctorHospitalMembership>();
    public ICollection<Patient> AssignedPatients { get; set; } = new List<Patient>();
}