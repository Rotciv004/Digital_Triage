using DigitalTriage.Domain.Entities;

namespace DigitalTriage.Application.Contracts.Services;

/// <summary>
/// Defines operations to manage patient medical data.
/// </summary>
public interface IMedicalDataService
{
    Task<MedicalData?> GetByPatientIdAsync(int patientId);
    Task UpdateAsync(MedicalData data);
    Task<MedicalFile> AddFileAsync(int medicalDataId, string fileName, string filePath);
    Task RemoveFileAsync(int fileId);
    Task<MedicalFile?> GetFileAsync(int fileId);
}
