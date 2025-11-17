using BCrypt.Net;
using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DigitalTriage.Infrastructure.Services;

/// <summary>
/// Provides patient-centric operations including registration, authentication, retrieval and lifecycle management.
/// </summary>
internal sealed class PatientService : IPatientService
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly ILogger<PatientService> _logger;
    private readonly IHospitalService _hospitalService;

    public PatientService(
        MedicalTriageDbContext dbContext,
        ILogger<PatientService> logger,
        IHospitalService hospitalService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _hospitalService = hospitalService;
    }

    public async Task<Patient?> GetByEmailAsync(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return await _dbContext.Patients
            .Include(p => p.PlaceOfBirth)
            .Include(p => p.Domicile)
            .Include(p => p.PreferredHospital)
            .Include(p => p.DoctorProfile)
            .FirstOrDefaultAsync(p => p.Email == email);
    }

    public async Task<Patient?> GetByIdAsync(int id)
    {
        return await _dbContext.Patients
            .Include(p => p.PlaceOfBirth)
            .Include(p => p.Domicile)
            .Include(p => p.PreferredHospital)
            .Include(p => p.DoctorProfile)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> IsEmailTakenAsync(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return await _dbContext.Patients.AnyAsync(p => p.Email == email);
    }

    public async Task<Patient> RegisterAsync(Patient patient, string password)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await EnsureEmailIsUnique(patient.Email);

        patient.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        await PersistAddressInformationAsync(patient);

        var doctorProfile = patient.DoctorProfile;
        if (doctorProfile != null)
        {
            doctorProfile.Specialization = doctorProfile.Specialization.Trim();
            patient.DoctorProfile = null;
        }

        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        if (doctorProfile != null)
        {
            doctorProfile.UserId = patient.Id;
            _dbContext.DoctorProfiles.Add(doctorProfile);
            await _dbContext.SaveChangesAsync();
            patient.DoctorProfile = doctorProfile;
        }

        var medicalData = new MedicalData
        {
            PatientId = patient.Id,
            LastVisitDate = DateTime.UtcNow
        };

        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        if (patient.DomicileId.HasValue || patient.Domicile != null)
        {
            var domicile = patient.Domicile ?? await _dbContext.Domiciles.FindAsync(patient.DomicileId);
            var closestHospital = await _hospitalService.FindClosestHospitalAsync(domicile);
            if (closestHospital != null)
            {
                patient.PreferredHospitalId = closestHospital.Id;
                _dbContext.Patients.Update(patient);
                await _dbContext.SaveChangesAsync();
            }
        }

        await transaction.CommitAsync();

        _logger.LogInformation("Registered patient {PatientId} with email {Email}", patient.Id, patient.Email);
        return patient;
    }

    public async Task<Patient?> AuthenticateAsync(string email, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var patient = await _dbContext.Patients
            .Include(p => p.DoctorProfile)
            .Include(p => p.PreferredHospital)
            .FirstOrDefaultAsync(p => p.Email == email);

        if (patient is null)
        {
            _logger.LogWarning("Authentication failed: no patient found for email {Email}", email);
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, patient.PasswordHash))
        {
            _logger.LogWarning("Authentication failed: invalid password for patient {PatientId}", patient.Id);
            return null;
        }

        patient.Role ??= patient.DoctorProfile != null ? "Doctor" : "Patient";
        return patient;
    }

    public async Task UpdateAsync(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        await PersistAddressInformationAsync(patient);

        _dbContext.Patients.Update(patient);

        if (patient.DoctorProfile != null)
        {
            if (patient.DoctorProfile.Id == 0)
            {
                patient.DoctorProfile.UserId = patient.Id;
                _dbContext.DoctorProfiles.Add(patient.DoctorProfile);
            }
            else
            {
                _dbContext.DoctorProfiles.Update(patient.DoctorProfile);
            }
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation("Updated patient {PatientId}", patient.Id);
    }

    public async Task<IReadOnlyList<Patient>> GetByHospitalIdsAsync(IEnumerable<int> hospitalIds)
    {
        ArgumentNullException.ThrowIfNull(hospitalIds);

        var ids = hospitalIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<Patient>();
        }

        return await _dbContext.Patients
            .Include(p => p.MedicalDatas).ThenInclude(md => md.Files)
            .Include(p => p.MedicalDatas).ThenInclude(md => md.AuthorizedDoctor).ThenInclude(d => d!.User)
            .Include(p => p.Issues)
            .Include(p => p.PlaceOfBirth)
            .Include(p => p.Domicile)
            .Include(p => p.PreferredHospital)
            .Include(p => p.DoctorProfile)
            .Where(p => p.PreferredHospitalId.HasValue && ids.Contains(p.PreferredHospitalId.Value))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Patient>> GetAllAsync()
    {
        var patients = await _dbContext.Patients
            .Include(p => p.MedicalDatas).ThenInclude(md => md.Files)
            .Include(p => p.MedicalDatas).ThenInclude(md => md.AuthorizedDoctor).ThenInclude(d => d!.User)
            .Include(p => p.Issues)
            .Include(p => p.PlaceOfBirth)
            .Include(p => p.Domicile)
            .Include(p => p.PreferredHospital)
            .Include(p => p.DoctorProfile)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();

        return patients;
    }

    public async Task DeleteAsync(int patientId)
    {
        var patient = await _dbContext.Patients
            .Include(p => p.PlaceOfBirth)
            .Include(p => p.Domicile)
            .Include(p => p.MedicalDatas)
            .Include(p => p.Issues)
            .FirstOrDefaultAsync(p => p.Id == patientId);

        if (patient == null)
        {
            _logger.LogWarning("Delete patient skipped: no patient found for id {PatientId}", patientId);
            return;
        }

        var placeOfBirthId = patient.PlaceOfBirthId;
        var domicileId = patient.DomicileId;

        _dbContext.Patients.Remove(patient);
        await _dbContext.SaveChangesAsync();

        await RemoveOrphanedAddressAsync(placeOfBirthId, domicileId);

        _logger.LogInformation("Deleted patient {PatientId}", patientId);
    }

    private async Task PersistAddressInformationAsync(Patient patient)
    {
        if (patient.PlaceOfBirth != null)
        {
            if (patient.PlaceOfBirth.Id == 0)
            {
                _dbContext.PlacesOfBirth.Add(patient.PlaceOfBirth);
                await _dbContext.SaveChangesAsync();
                patient.PlaceOfBirthId = patient.PlaceOfBirth.Id;
            }
            else
            {
                _dbContext.PlacesOfBirth.Update(patient.PlaceOfBirth);
            }
        }

        if (patient.Domicile != null)
        {
            if (patient.Domicile.Id == 0)
            {
                _dbContext.Domiciles.Add(patient.Domicile);
                await _dbContext.SaveChangesAsync();
                patient.DomicileId = patient.Domicile.Id;
            }
            else
            {
                _dbContext.Domiciles.Update(patient.Domicile);
            }
        }
    }

    private async Task RemoveOrphanedAddressAsync(int? placeOfBirthId, int? domicileId)
    {
        if (placeOfBirthId.HasValue && await IsPlaceOfBirthOrphaned(placeOfBirthId.Value))
        {
            var place = await _dbContext.PlacesOfBirth.FindAsync(placeOfBirthId.Value);
            if (place != null)
            {
                _dbContext.PlacesOfBirth.Remove(place);
                await _dbContext.SaveChangesAsync();
            }
        }

        if (domicileId.HasValue && await IsDomicileOrphaned(domicileId.Value))
        {
            var domicile = await _dbContext.Domiciles.FindAsync(domicileId.Value);
            if (domicile != null)
            {
                _dbContext.Domiciles.Remove(domicile);
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    private async Task<bool> IsPlaceOfBirthOrphaned(int placeOfBirthId)
    {
        return !await _dbContext.Patients.AnyAsync(p => p.PlaceOfBirthId == placeOfBirthId);
    }

    private async Task<bool> IsDomicileOrphaned(int domicileId)
    {
        return !await _dbContext.Patients.AnyAsync(p => p.DomicileId == domicileId);
    }

    private async Task EnsureEmailIsUnique(string email)
    {
        if (await _dbContext.Patients.AnyAsync(p => p.Email == email))
        {
            throw new InvalidOperationException($"A patient with email '{email}' already exists.");
        }
    }
}

