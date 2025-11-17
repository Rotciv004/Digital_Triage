using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalTriage.Infrastructure.Services;

internal sealed class MedicalDataAuthorizationService : IMedicalDataAuthorizationService
{
    private readonly MedicalTriageDbContext _dbContext;

    public MedicalDataAuthorizationService(MedicalTriageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MedicalData?> GetOwnedMedicalDataAsync(int userId, int medicalDataId)
    {
        return await _dbContext.MedicalDatas
            .AsNoTracking()
            .FirstOrDefaultAsync(md => md.Id == medicalDataId && md.PatientId == userId);
    }

    public async Task<bool> CanUserAccessMedicalDataAsync(int userId, MedicalData medicalData)
    {
        if (medicalData.PatientId == userId)
        {
            return true;
        }

        if (medicalData.AuthorizedDoctorId == null)
        {
            return false;
        }

        var doctorProfileId = await _dbContext.DoctorProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (int?)profile.Id)
            .FirstOrDefaultAsync();

        return doctorProfileId.HasValue && doctorProfileId.Value == medicalData.AuthorizedDoctorId;
    }
}

