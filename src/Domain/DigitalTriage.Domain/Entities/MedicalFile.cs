using System.ComponentModel.DataAnnotations;

namespace DigitalTriage.Domain.Entities;

public class MedicalFile
{
    public int Id { get; set; }

    [Required]
    public int MedicalDataId { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string FilePath { get; set; } = string.Empty;

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    public MedicalData? MedicalData { get; set; }
}
