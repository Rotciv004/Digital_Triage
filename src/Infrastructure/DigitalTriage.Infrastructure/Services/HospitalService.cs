using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DigitalTriage.Infrastructure.Services;

/// <summary>
/// Provides hospital management operations for doctors and patients.
/// </summary>
internal sealed class HospitalService : IHospitalService
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly ILogger<HospitalService> _logger;

    public HospitalService(MedicalTriageDbContext dbContext, ILogger<HospitalService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Hospital> CreateHospitalAsync(int doctorUserId, Hospital hospital)
    {
        ArgumentNullException.ThrowIfNull(hospital);

        var doctorProfile = await EnsureDoctorProfileAsync(doctorUserId);

        hospital.Name = hospital.Name.Trim();
        hospital.CreatedByDoctorId = doctorProfile.Id;

        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Doctor user {DoctorUserId} created hospital {HospitalName} (ID: {HospitalId})", doctorUserId, hospital.Name, hospital.Id);
        return hospital;
    }

    public async Task UpdateHospitalAsync(int doctorUserId, Hospital hospital)
    {
        ArgumentNullException.ThrowIfNull(hospital);

        var doctorProfile = await EnsureDoctorProfileAsync(doctorUserId);
        var existingHospital = await _dbContext.Hospitals.FirstOrDefaultAsync(h => h.Id == hospital.Id)
            ?? throw new InvalidOperationException("Hospital not found.");

        if (!await IsDoctorActiveInHospitalAsync(doctorProfile.Id, hospital.Id))
        {
            throw new UnauthorizedAccessException("Doctor is not an active member of this hospital.");
        }

        existingHospital.Name = hospital.Name.Trim();
        existingHospital.Country = hospital.Country;
        existingHospital.County = hospital.County;
        existingHospital.City = hospital.City;
        existingHospital.Street = hospital.Street;
        existingHospital.Number = hospital.Number;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Doctor user {DoctorUserId} updated hospital {HospitalId}", doctorUserId, hospital.Id);
    }

    public async Task LeaveHospitalAsync(int doctorUserId, int hospitalId)
    {
        var doctorProfile = await EnsureDoctorProfileAsync(doctorUserId);
        var membership = await _dbContext.DoctorHospitalMemberships
            .FirstOrDefaultAsync(m => m.DoctorId == doctorProfile.Id && m.HospitalId == hospitalId && m.IsActive)
            ?? throw new InvalidOperationException("Doctor is not an active member of the specified hospital.");

        membership.IsActive = false;
        membership.LeftAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Doctor user {DoctorUserId} left hospital {HospitalId}", doctorUserId, hospitalId);

        var hasOtherMembers = await _dbContext.DoctorHospitalMemberships
            .AnyAsync(m => m.HospitalId == hospitalId && m.IsActive);

        if (!hasOtherMembers)
        {
            var hospital = await _dbContext.Hospitals.FirstOrDefaultAsync(h => h.Id == hospitalId);
            if (hospital != null)
            {
                _dbContext.Hospitals.Remove(hospital);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Hospital {HospitalId} deleted because it no longer had active doctors", hospitalId);
            }
        }
    }

    public async Task JoinHospitalAsync(int doctorUserId, int hospitalId)
    {
        var doctorProfile = await EnsureDoctorProfileAsync(doctorUserId);
        _ = await _dbContext.Hospitals
            .FirstOrDefaultAsync(h => h.Id == hospitalId)
            ?? throw new InvalidOperationException("Hospital not found.");

        var membership = await _dbContext.DoctorHospitalMemberships
            .FirstOrDefaultAsync(m => m.DoctorId == doctorProfile.Id && m.HospitalId == hospitalId);

        if (membership == null)
        {
            membership = new DoctorHospitalMembership
            {
                DoctorId = doctorProfile.Id,
                HospitalId = hospitalId,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };
            _dbContext.DoctorHospitalMemberships.Add(membership);
        }
        else
        {
            membership.IsActive = true;
            membership.JoinedAt = DateTime.UtcNow;
            membership.LeftAt = null;
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Doctor user {DoctorUserId} joined hospital {HospitalId}", doctorUserId, hospitalId);
    }

    public async Task<IReadOnlyList<Hospital>> GetHospitalsForDoctorAsync(int doctorUserId)
    {
        var doctorProfile = await EnsureDoctorProfileAsync(doctorUserId);

        var hospitals = await _dbContext.DoctorHospitalMemberships
            .Where(m => m.DoctorId == doctorProfile.Id && m.IsActive)
            .Select(m => m.Hospital)
            .OrderBy(h => h.Name)
            .ToListAsync();

        return hospitals;
    }

    public async Task<IReadOnlyList<Hospital>> SearchHospitalsAsync(string? searchTerm, string? country = null, string? county = null, string? city = null, int maxResults = 20)
    {
        IQueryable<Hospital> query = _dbContext.Hospitals;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm.Trim()}%";
            query = query.Where(h =>
                EF.Functions.Like(h.Name, term) ||
                (h.City != null && EF.Functions.Like(h.City, term)) ||
                (h.County != null && EF.Functions.Like(h.County, term)) ||
                (h.Country != null && EF.Functions.Like(h.Country, term)));
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            var countryTerm = $"%{country.Trim()}%";
            query = query.Where(h => h.Country != null && EF.Functions.Like(h.Country, countryTerm));
        }

        if (!string.IsNullOrWhiteSpace(county))
        {
            var countyTerm = $"%{county.Trim()}%";
            query = query.Where(h => h.County != null && EF.Functions.Like(h.County, countyTerm));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityTerm = $"%{city.Trim()}%";
            query = query.Where(h => h.City != null && EF.Functions.Like(h.City, cityTerm));
        }

        var hospitals = await query
            .OrderBy(h => h.Name)
            .Take(Math.Max(1, maxResults))
            .ToListAsync();

        return hospitals;
    }

    public async Task<IReadOnlyList<Hospital>> GetAllAsync()
    {
        var hospitals = await _dbContext.Hospitals
            .OrderBy(h => h.Name)
            .ToListAsync();

        return hospitals;
    }

    public Task<Hospital?> GetByIdAsync(int hospitalId)
    {
        return _dbContext.Hospitals.FirstOrDefaultAsync(h => h.Id == hospitalId);
    }

    public Task<Hospital?> GetHospitalWithDoctorsAsync(int hospitalId)
    {
        return _dbContext.Hospitals
            .Include(h => h.DoctorMemberships.Where(m => m.IsActive))
                .ThenInclude(m => m.Doctor)
                    .ThenInclude(d => d.User)
            .FirstOrDefaultAsync(h => h.Id == hospitalId);
    }

    public async Task<Hospital?> FindClosestHospitalAsync(Domicile? domicile)
    {
        var hospitals = await _dbContext.Hospitals.ToListAsync();
        if (hospitals.Count == 0)
        {
            return null;
        }

        if (domicile == null)
        {
            return hospitals.First();
        }

        var rankedHospital = hospitals
            .Select(h => new
            {
                Hospital = h,
                Score = CalculateMatchScore(h, domicile)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Hospital.Id)
            .FirstOrDefault();

        return rankedHospital == null || rankedHospital.Score == 0
            ? hospitals.First()
            : rankedHospital.Hospital;
    }

    public async Task SetPatientPreferredHospitalAsync(int patientId, int hospitalId)
    {
        var patient = await _dbContext.Patients.FirstOrDefaultAsync(p => p.Id == patientId)
            ?? throw new InvalidOperationException("Patient not found.");

        _ = await _dbContext.Hospitals.FirstOrDefaultAsync(h => h.Id == hospitalId)
            ?? throw new InvalidOperationException("Hospital not found.");

        patient.PreferredHospitalId = hospitalId;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Patient {PatientId} assigned to hospital {HospitalId}", patientId, hospitalId);
    }

    private async Task<DoctorProfile> EnsureDoctorProfileAsync(int doctorUserId)
    {
        var profile = await _dbContext.DoctorProfiles
            .FirstOrDefaultAsync(dp => dp.UserId == doctorUserId);

        if (profile != null)
        {
            return profile;
        }

        var doctorAccount = await _dbContext.Patients
            .Include(p => p.DoctorProfile)
            .FirstOrDefaultAsync(p => p.Id == doctorUserId)
            ?? throw new InvalidOperationException("Doctor account not found.");

        profile = doctorAccount.DoctorProfile ?? new DoctorProfile
        {
            UserId = doctorAccount.Id,
            Specialization = string.IsNullOrWhiteSpace(doctorAccount.DoctorProfile?.Specialization)
                ? "General Medicine"
                : doctorAccount.DoctorProfile!.Specialization.Trim()
        };

        if (doctorAccount.DoctorProfile == null)
        {
            doctorAccount.DoctorProfile = profile;
            _dbContext.DoctorProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();
        }

        return profile;
    }

    private async Task<bool> IsDoctorActiveInHospitalAsync(int doctorProfileId, int hospitalId)
    {
        return await _dbContext.DoctorHospitalMemberships.AnyAsync(m =>
            m.DoctorId == doctorProfileId &&
            m.HospitalId == hospitalId &&
            m.IsActive);
    }

    private static int CalculateMatchScore(Hospital hospital, Domicile domicile)
    {
        static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

        int score = 0;

        if (Normalize(domicile.Country) == Normalize(hospital.Country))
        {
            score += 1;
        }

        if (Normalize(domicile.County) == Normalize(hospital.County))
        {
            score += 2;
        }

        if (Normalize(domicile.City) == Normalize(hospital.City))
        {
            score += 3;
        }

        if (Normalize(domicile.Street) == Normalize(hospital.Street))
        {
            score += 4;
        }

        if (Normalize(domicile.Number) == Normalize(hospital.Number))
        {
            score += 5;
        }

        return score;
    }
}

