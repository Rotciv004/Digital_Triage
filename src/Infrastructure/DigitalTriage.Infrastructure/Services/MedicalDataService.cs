using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalTriage.Infrastructure.Services;

/// <summary>
/// Provides medical data retrieval and persistence services.
/// </summary>
internal sealed class MedicalDataService : IMedicalDataService
{
    private readonly MedicalTriageDbContext _dbContext;

    public MedicalDataService(MedicalTriageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<MedicalData?> GetByPatientIdAsync(int patientId)
    {
        return _dbContext.MedicalDatas
            .Include(m => m.Files)
            .Include(m => m.AuthorizedDoctor)
                .ThenInclude(d => d!.User)
            .FirstOrDefaultAsync(m => m.PatientId == patientId);
    }

    public async Task UpdateAsync(MedicalData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        data.UpdatedAt = DateTime.UtcNow;
        _dbContext.MedicalDatas.Update(data);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<MedicalFile> AddFileAsync(int medicalDataId, string fileName, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var file = new MedicalFile
        {
            MedicalDataId = medicalDataId,
            FileName = fileName,
            FilePath = filePath,
            UploadDate = DateTime.UtcNow
        };

        _dbContext.MedicalFiles.Add(file);
        await _dbContext.SaveChangesAsync();
        return file;
    }

    public async Task RemoveFileAsync(int fileId)
    {
        var file = await _dbContext.MedicalFiles.FindAsync(fileId);
        if (file == null)
        {
            return;
        }

        _dbContext.MedicalFiles.Remove(file);
        await _dbContext.SaveChangesAsync();
    }

    public Task<MedicalFile?> GetFileAsync(int fileId)
    {
        return _dbContext.MedicalFiles
            .Include(f => f.MedicalData)
            .FirstOrDefaultAsync(f => f.Id == fileId);
    }
}
