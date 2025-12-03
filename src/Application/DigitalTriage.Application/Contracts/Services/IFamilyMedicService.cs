using DigitalTriage.Domain.Entities;

namespace DigitalTriage.Application.Contracts.Services;

/// <summary>
/// Service for managing family medic assignments and requests.
/// </summary>
public interface IFamilyMedicService
{
    /// <summary>
    /// Request a doctor to become the patient's family medic.
    /// </summary>
    Task<FamilyMedicRequest> RequestFamilyMedicAsync(int patientId, string doctorEmail);

    /// <summary>
    /// Accept a family medic request.
    /// </summary>
    Task<bool> AcceptFamilyMedicRequestAsync(int requestId, int doctorId);

    /// <summary>
    /// Reject a family medic request.
    /// </summary>
    Task<bool> RejectFamilyMedicRequestAsync(int requestId, int doctorId);

    /// <summary>
    /// Get all pending requests for a doctor by email.
    /// </summary>
    Task<IEnumerable<FamilyMedicRequest>> GetPendingRequestsForDoctorAsync(string doctorEmail);

    /// <summary>
    /// Get all family patients for a doctor.
    /// </summary>
    Task<IEnumerable<Patient>> GetFamilyPatientsAsync(int doctorId);

    /// <summary>
    /// Get the current family medic request for a patient.
    /// </summary>
    Task<FamilyMedicRequest?> GetCurrentRequestForPatientAsync(int patientId);

    /// <summary>
    /// Remove the family medic assignment (patient can change family medic).
    /// </summary>
    Task<bool> RemoveFamilyMedicAsync(int patientId);

    /// <summary>
    /// Store Outlook token for a patient (will be encrypted internally).
    /// </summary>
    Task<bool> StoreOutlookTokenAsync(int patientId, string refreshToken, DateTimeOffset expiresAt);

    /// <summary>
    /// Get Outlook token for a patient (decrypted).
    /// </summary>
    Task<string?> GetOutlookTokenAsync(int patientId);
}

