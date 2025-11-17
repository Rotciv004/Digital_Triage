namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents the relationship between a doctor and a hospital, including membership status.
/// </summary>
public class DoctorHospitalMembership
{
    public int Id { get; set; }

    public int DoctorId { get; set; }
    public DoctorProfile Doctor { get; set; } = default!;

    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = default!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public bool IsActive { get; set; } = true;
}
