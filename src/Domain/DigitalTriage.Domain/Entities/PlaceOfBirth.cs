using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents the place of birth information for a patient.
/// </summary>
public class PlaceOfBirth
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(100)]
    public string? County { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }
}
