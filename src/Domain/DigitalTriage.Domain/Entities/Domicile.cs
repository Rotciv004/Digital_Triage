using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

/// <summary>
/// Represents the domicile (address) information of a patient.
/// </summary>
public class Domicile
{
    public int Id { get; set; }

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
}
