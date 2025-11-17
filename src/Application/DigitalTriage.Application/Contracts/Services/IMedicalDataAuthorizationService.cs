using DigitalTriage.Domain.Entities;

namespace DigitalTriage.Application.Contracts.Services;

/// <summary>
/// Provides authorization helpers for medical data access.
/// </summary>
public interface IMedicalDataAuthorizationService
{
    Task<MedicalData?> GetOwnedMedicalDataAsync(int userId, int medicalDataId);
    Task<bool> CanUserAccessMedicalDataAsync(int userId, MedicalData medicalData);
}

