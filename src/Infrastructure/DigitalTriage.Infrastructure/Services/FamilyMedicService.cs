using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace DigitalTriage.Infrastructure.Services;

/// <summary>
/// Service for managing family medic assignments and requests.
/// </summary>
internal sealed class FamilyMedicService : IFamilyMedicService
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly ILogger<FamilyMedicService> _logger;
    private readonly IEmailService _emailService;
    private readonly IPatientService _patientService;
    private readonly byte[] _encryptionKey; // In production, store this securely (e.g., Azure Key Vault)

    public FamilyMedicService(
        MedicalTriageDbContext dbContext,
        ILogger<FamilyMedicService> logger,
        IEmailService emailService,
        IPatientService patientService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _emailService = emailService;
        _patientService = patientService;
        
        // Get encryption key from configuration (for production, use Azure Key Vault or similar)
        var key = configuration["Encryption:Key"] ?? "DefaultEncryptionKey32Bytes!!"; // 32 bytes for AES-256
        _encryptionKey = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
    }

    public async Task<FamilyMedicRequest> RequestFamilyMedicAsync(int patientId, string doctorEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(doctorEmail);

        var patient = await _patientService.GetByIdAsync(patientId);
        if (patient == null)
        {
            throw new InvalidOperationException($"Patient with ID {patientId} not found.");
        }

        // Check if patient already has a pending request
        var existingPending = await _dbContext.FamilyMedicRequests
            .FirstOrDefaultAsync(r => r.PatientId == patientId && r.Status == "Pending");

        if (existingPending != null)
        {
            throw new InvalidOperationException("You already have a pending family medic request.");
        }

        // If patient already has a family medic, remove it first (allowing change)
        if (patient.FamilyMedicDoctorId.HasValue)
        {
            patient.FamilyMedicEmail = null;
            patient.FamilyMedicDoctorId = null;
            await _dbContext.SaveChangesAsync();
        }

        // Check if doctor exists
        var doctor = await _dbContext.Patients
            .Include(p => p.DoctorProfile)
            .FirstOrDefaultAsync(p => p.Email == doctorEmail && p.DoctorProfile != null);

        if (doctor == null)
        {
            throw new InvalidOperationException($"No doctor found with email {doctorEmail}.");
        }

        var request = new FamilyMedicRequest
        {
            PatientId = patientId,
            DoctorEmail = doctorEmail,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };

        _dbContext.FamilyMedicRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        // Send notification email to doctor
        var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(patientName))
        {
            patientName = patient.Email;
        }

        await _emailService.SendFamilyMedicRequestNotificationAsync(
            doctorEmail,
            patientName,
            patient.Email);

        _logger.LogInformation(
            "Family medic request created: Patient {PatientId} requested doctor {DoctorEmail}",
            patientId,
            doctorEmail);

        return request;
    }

    public async Task<bool> AcceptFamilyMedicRequestAsync(int requestId, int doctorId)
    {
        var request = await _dbContext.FamilyMedicRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            _logger.LogWarning("Family medic request {RequestId} not found", requestId);
            return false;
        }

        if (request.Status != "Pending")
        {
            _logger.LogWarning("Family medic request {RequestId} is not pending (status: {Status})", requestId, request.Status);
            return false;
        }

        // Verify doctor email matches
        var doctor = await _dbContext.DoctorProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor == null || !string.Equals(doctor.User.Email, request.DoctorEmail, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Doctor {DoctorId} does not match request {RequestId} email", doctorId, requestId);
            return false;
        }

        // Update request
        request.Status = "Accepted";
        request.RespondedAt = DateTime.UtcNow;
        request.DoctorId = doctorId;

        // Update patient
        request.Patient!.FamilyMedicEmail = request.DoctorEmail;
        request.Patient.FamilyMedicDoctorId = doctorId;

        // Reject any other pending requests for this patient
        var otherPending = await _dbContext.FamilyMedicRequests
            .Where(r => r.PatientId == request.PatientId && r.Id != requestId && r.Status == "Pending")
            .ToListAsync();

        foreach (var other in otherPending)
        {
            other.Status = "Rejected";
            other.RespondedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Family medic request {RequestId} accepted by doctor {DoctorId}",
            requestId,
            doctorId);

        return true;
    }

    public async Task<bool> RejectFamilyMedicRequestAsync(int requestId, int doctorId)
    {
        var request = await _dbContext.FamilyMedicRequests
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null || request.Status != "Pending")
        {
            return false;
        }

        // Verify doctor email matches
        var doctor = await _dbContext.DoctorProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == doctorId);

        if (doctor == null || !string.Equals(doctor.User.Email, request.DoctorEmail, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        request.Status = "Rejected";
        request.RespondedAt = DateTime.UtcNow;
        request.DoctorId = doctorId;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Family medic request {RequestId} rejected by doctor {DoctorId}",
            requestId,
            doctorId);

        return true;
    }

    public async Task<IEnumerable<FamilyMedicRequest>> GetPendingRequestsForDoctorAsync(string doctorEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(doctorEmail);

        return await _dbContext.FamilyMedicRequests
            .Include(r => r.Patient)
            .Where(r => r.DoctorEmail == doctorEmail && r.Status == "Pending")
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Patient>> GetFamilyPatientsAsync(int doctorId)
    {
        return await _dbContext.Patients
            .Include(p => p.PlaceOfBirth)
            .Include(p => p.Domicile)
            .Include(p => p.MedicalDatas).ThenInclude(md => md.Files)
            .Include(p => p.Issues)
            .Where(p => p.FamilyMedicDoctorId == doctorId)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();
    }

    public async Task<FamilyMedicRequest?> GetCurrentRequestForPatientAsync(int patientId)
    {
        return await _dbContext.FamilyMedicRequests
            .Include(r => r.Doctor).ThenInclude(d => d!.User)
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> RemoveFamilyMedicAsync(int patientId)
    {
        var patient = await _patientService.GetByIdAsync(patientId);
        if (patient == null)
        {
            return false;
        }

        patient.FamilyMedicEmail = null;
        patient.FamilyMedicDoctorId = null;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Family medic removed for patient {PatientId}", patientId);
        return true;
    }

    public async Task<bool> StoreOutlookTokenAsync(int patientId, string refreshToken, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            // If token is empty, remove the stored token
            var existingRequest = await _dbContext.FamilyMedicRequests
                .Where(r => r.PatientId == patientId && r.OutlookToken != null)
                .OrderByDescending(r => r.RequestedAt)
                .FirstOrDefaultAsync();

            if (existingRequest != null)
            {
                existingRequest.OutlookToken = null;
                existingRequest.TokenExpiresAt = null;
                await _dbContext.SaveChangesAsync();
            }
            return true;
        }

        var request = await _dbContext.FamilyMedicRequests
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();

        if (request == null)
        {
            // Create a new request record to store the token
            var patient = await _patientService.GetByIdAsync(patientId);
            if (patient == null)
            {
                return false;
            }

            request = new FamilyMedicRequest
            {
                PatientId = patientId,
                DoctorEmail = patient.FamilyMedicEmail ?? string.Empty,
                Status = "Accepted", // Token storage doesn't require a request
                RequestedAt = DateTime.UtcNow
            };
            _dbContext.FamilyMedicRequests.Add(request);
        }

        // Encrypt the token before storing
        request.OutlookToken = EncryptToken(refreshToken);
        request.TokenExpiresAt = expiresAt.UtcDateTime;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GetOutlookTokenAsync(int patientId)
    {
        var request = await _dbContext.FamilyMedicRequests
            .Where(r => r.PatientId == patientId && r.OutlookToken != null)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();

        if (request?.OutlookToken == null)
        {
            return null;
        }

        // Decrypt the token before returning
        try
        {
            return DecryptToken(request.OutlookToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Outlook token for patient {PatientId}", patientId);
            return null;
        }
    }

    private string EncryptToken(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private string DecryptToken(string cipherText)
    {
        var fullCipher = Convert.FromBase64String(cipherText);
        var iv = new byte[16];
        Array.Copy(fullCipher, 0, iv, 0, iv.Length);
        var cipher = new byte[fullCipher.Length - 16];
        Array.Copy(fullCipher, 16, cipher, 0, cipher.Length);

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(cipher);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);

        return sr.ReadToEnd();
    }
}

