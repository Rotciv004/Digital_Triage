using DigitalTriage.Domain.Entities;

namespace DigitalTriage.Application.Contracts.Services;

/// <summary>
/// Defines operations for managing patients including registration, authentication and updates.
/// </summary>
public interface IPatientService
{
    Task<Patient?> GetByEmailAsync(string email);
    Task<Patient?> GetByIdAsync(int id);
    Task<bool> IsEmailTakenAsync(string email);
    Task<Patient> RegisterAsync(Patient patient, string password);
    Task<Patient?> AuthenticateAsync(string email, string password);
    Task UpdateAsync(Patient patient);
    Task<IReadOnlyList<Patient>> GetByHospitalIdsAsync(IEnumerable<int> hospitalIds);
    Task<IReadOnlyList<Patient>> GetAllAsync();
    Task DeleteAsync(int patientId);
}
