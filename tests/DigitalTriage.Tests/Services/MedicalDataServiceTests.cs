using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using DigitalTriage.Infrastructure.Services;
using DigitalTriage.Tests.Helpers;
using DigitalTriage.Tests.TestFixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DigitalTriage.Tests.Services;

public class MedicalDataServiceTests : IDisposable
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly IMedicalDataService _medicalDataService;

    public MedicalDataServiceTests()
    {
        var fixture = new DatabaseFixture();
        _dbContext = fixture.CreateDbContext();
        _medicalDataService = new MedicalDataService(_dbContext);
    }

    [Fact]
    public async Task GetByPatientIdAsync_WithExistingPatient_ReturnsMedicalData()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _medicalDataService.GetByPatientIdAsync(patient.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(patient.Id, result.PatientId);
    }

    [Fact]
    public async Task GetByPatientIdAsync_WithNonExistentPatient_ReturnsNull()
    {
        // Act
        var result = await _medicalDataService.GetByPatientIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesMedicalData()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        medicalData.BloodType = "A+";
        medicalData.Allergies = "Peanuts, Shellfish";
        medicalData.Symptoms = "Chest pain, Shortness of breath";

        // Act
        await _medicalDataService.UpdateAsync(medicalData);

        // Assert
        var updated = await _dbContext.MedicalDatas.FindAsync(medicalData.Id);
        Assert.NotNull(updated);
        Assert.Equal("A+", updated.BloodType);
        Assert.Equal("Peanuts, Shellfish", updated.Allergies);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task AddFileAsync_WithValidData_CreatesMedicalFile()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _medicalDataService.AddFileAsync(
            medicalData.Id,
            "test_report.pdf",
            "uploads/test_report.pdf");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("test_report.pdf", result.FileName);
        Assert.Equal(medicalData.Id, result.MedicalDataId);

        var fileInDb = await _dbContext.MedicalFiles.FindAsync(result.Id);
        Assert.NotNull(fileInDb);
    }

    [Fact]
    public async Task RemoveFileAsync_WithExistingFile_RemovesFile()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        var file = MockDataBuilder.CreateMedicalFile(medicalData.Id);
        _dbContext.MedicalFiles.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act
        await _medicalDataService.RemoveFileAsync(file.Id);

        // Assert
        var removed = await _dbContext.MedicalFiles.FindAsync(file.Id);
        Assert.Null(removed);
    }

    [Fact]
    public async Task RemoveFileAsync_WithNonExistentFile_DoesNotThrow()
    {
        // Act & Assert
        await _medicalDataService.RemoveFileAsync(99999);
        // Should not throw
    }

    [Fact]
    public async Task GetFileAsync_WithExistingFile_ReturnsFile()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        var file = MockDataBuilder.CreateMedicalFile(medicalData.Id, "test_file.pdf");
        _dbContext.MedicalFiles.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _medicalDataService.GetFileAsync(file.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(file.Id, result.Id);
        Assert.NotNull(result.MedicalData);
    }

    [Fact]
    public async Task GetFileAsync_WithNonExistentFile_ReturnsNull()
    {
        // Act
        var result = await _medicalDataService.GetFileAsync(99999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByPatientIdAsync_IncludesFiles()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        var file1 = MockDataBuilder.CreateMedicalFile(medicalData.Id, "file1.pdf");
        var file2 = MockDataBuilder.CreateMedicalFile(medicalData.Id, "file2.pdf");
        _dbContext.MedicalFiles.AddRange(file1, file2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _medicalDataService.GetByPatientIdAsync(patient.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Files.Count);
    }

    [Fact]
    public async Task AddFileAsync_WithEmptyFileName_ThrowsException()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _medicalDataService.AddFileAsync(medicalData.Id, "", "path/to/file.pdf"));
    }

    [Fact]
    public async Task AddFileAsync_WithEmptyFilePath_ThrowsException()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _medicalDataService.AddFileAsync(medicalData.Id, "file.pdf", ""));
    }

    [Fact]
    public async Task UpdateAsync_WithNullMedicalData_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _medicalDataService.UpdateAsync(null!));
    }

    [Fact]
    public async Task GetFileAsync_IncludesMedicalData()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        var file = MockDataBuilder.CreateMedicalFile(medicalData.Id, "test_file.pdf");
        _dbContext.MedicalFiles.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _medicalDataService.GetFileAsync(file.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.MedicalData);
        Assert.Equal(medicalData.Id, result.MedicalData.Id);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

