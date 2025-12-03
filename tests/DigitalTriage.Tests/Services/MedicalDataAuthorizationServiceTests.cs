using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using DigitalTriage.Infrastructure.Services;
using DigitalTriage.Tests.Helpers;
using DigitalTriage.Tests.TestFixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DigitalTriage.Tests.Services;

public class MedicalDataAuthorizationServiceTests : IDisposable
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly IMedicalDataAuthorizationService _authorizationService;

    public MedicalDataAuthorizationServiceTests()
    {
        var fixture = new DatabaseFixture();
        _dbContext = fixture.CreateDbContext();
        _authorizationService = new MedicalDataAuthorizationService(_dbContext);
    }

    [Fact]
    public async Task GetOwnedMedicalDataAsync_WithOwnData_ReturnsMedicalData()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authorizationService.GetOwnedMedicalDataAsync(patient.Id, medicalData.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(patient.Id, result.PatientId);
    }

    [Fact]
    public async Task GetOwnedMedicalDataAsync_WithOtherPatientData_ReturnsNull()
    {
        // Arrange
        var patient1 = MockDataBuilder.CreatePatient();
        var patient2 = MockDataBuilder.CreatePatient();
        _dbContext.Patients.AddRange(patient1, patient2);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient1.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authorizationService.GetOwnedMedicalDataAsync(patient2.Id, medicalData.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CanUserAccessMedicalDataAsync_WithOwnData_ReturnsTrue()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanUserAccessMedicalDataAsync(patient.Id, medicalData);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanUserAccessMedicalDataAsync_WithAuthorizedDoctor_ReturnsTrue()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(
            patient.Id,
            authorizedDoctorId: doctorProfile.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanUserAccessMedicalDataAsync(doctor.Id, medicalData);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanUserAccessMedicalDataAsync_WithUnauthorizedDoctor_ReturnsFalse()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor1 = MockDataBuilder.CreatePatient(asDoctor: true);
        var doctor2 = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor1, doctor2);
        await _dbContext.SaveChangesAsync();

        var doctorProfile1 = MockDataBuilder.CreateDoctorProfile(doctor1.Id);
        var doctorProfile2 = MockDataBuilder.CreateDoctorProfile(doctor2.Id);
        _dbContext.DoctorProfiles.AddRange(doctorProfile1, doctorProfile2);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(
            patient.Id,
            authorizedDoctorId: doctorProfile1.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanUserAccessMedicalDataAsync(doctor2.Id, medicalData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanUserAccessMedicalDataAsync_WithNoAuthorizedDoctor_ReturnsFalse()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(
            patient.Id,
            authorizedDoctorId: null);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanUserAccessMedicalDataAsync(doctor.Id, medicalData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanUserAccessMedicalDataAsync_WithNonDoctorUser_ReturnsFalse()
    {
        // Arrange
        var patient1 = MockDataBuilder.CreatePatient();
        var patient2 = MockDataBuilder.CreatePatient();
        _dbContext.Patients.AddRange(patient1, patient2);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient1.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanUserAccessMedicalDataAsync(patient2.Id, medicalData);

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

