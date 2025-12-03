using BCrypt.Net;
using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using DigitalTriage.Infrastructure.Services;
using DigitalTriage.Tests.Helpers;
using DigitalTriage.Tests.TestFixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DigitalTriage.Tests.Services;

public class PatientServiceTests : IDisposable
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly Mock<IHospitalService> _hospitalServiceMock;
    private readonly IPatientService _patientService;

    public PatientServiceTests()
    {
        var fixture = new DatabaseFixture();
        _dbContext = fixture.CreateDbContext();
        _hospitalServiceMock = new Mock<IHospitalService>();
        var logger = TestLoggerFactory.Create<PatientService>();
        _patientService = new PatientService(_dbContext, logger, _hospitalServiceMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesPatient()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient(
            email: "newpatient@test.com",
            password: "SecurePassword123!",
            firstName: "John",
            lastName: "Doe");

        _hospitalServiceMock.Setup(x => x.FindClosestHospitalAsync(It.IsAny<Domicile>()))
            .ReturnsAsync((Hospital?)null);

        // Act
        var result = await _patientService.RegisterAsync(patient, "SecurePassword123!");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("newpatient@test.com", result.Email);
        Assert.True(BCrypt.Net.BCrypt.Verify("SecurePassword123!", result.PasswordHash));
        Assert.NotNull(await _dbContext.Patients.FindAsync(result.Id));
        
        // Verify medical data was created
        var medicalData = await _dbContext.MedicalDatas
            .FirstOrDefaultAsync(md => md.PatientId == result.Id);
        Assert.NotNull(medicalData);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsException()
    {
        // Arrange
        var existingPatient = MockDataBuilder.CreatePatient(email: "existing@test.com");
        _dbContext.Patients.Add(existingPatient);
        await _dbContext.SaveChangesAsync();

        var newPatient = MockDataBuilder.CreatePatient(email: "existing@test.com");

        _hospitalServiceMock.Setup(x => x.FindClosestHospitalAsync(It.IsAny<Domicile>()))
            .ReturnsAsync((Hospital?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _patientService.RegisterAsync(newPatient, "Password123!"));
    }

    [Fact]
    public async Task RegisterAsync_WithDoctorProfile_CreatesDoctorProfile()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient(
            email: "doctor@test.com",
            asDoctor: true);

        patient.DoctorProfile!.Specialization = "Neurology";

        _hospitalServiceMock.Setup(x => x.FindClosestHospitalAsync(It.IsAny<Domicile>()))
            .ReturnsAsync((Hospital?)null);

        // Act
        var result = await _patientService.RegisterAsync(patient, "Password123!");

        // Assert
        Assert.NotNull(result.DoctorProfile);
        Assert.Equal("Neurology", result.DoctorProfile.Specialization);
        Assert.Equal(result.Id, result.DoctorProfile.UserId);
    }

    [Fact]
    public async Task RegisterAsync_WithDomicile_AssignsClosestHospital()
    {
        // Arrange
        var hospital = MockDataBuilder.CreateHospital(name: "Closest Hospital");
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        var domicile = MockDataBuilder.CreateDomicile();
        _dbContext.Domiciles.Add(domicile);
        await _dbContext.SaveChangesAsync();

        var patient = MockDataBuilder.CreatePatient(
            email: "patient@test.com",
            withDomicile: false);
        patient.DomicileId = domicile.Id;
        patient.Domicile = null; // Don't include the navigation property

        _hospitalServiceMock.Setup(x => x.FindClosestHospitalAsync(It.IsAny<Domicile>()))
            .ReturnsAsync((Domicile d) => hospital);

        // Act
        var result = await _patientService.RegisterAsync(patient, "Password123!");

        // Assert
        var savedPatient = await _dbContext.Patients
            .Include(p => p.Domicile)
            .FirstOrDefaultAsync(p => p.Id == result.Id);
        Assert.NotNull(savedPatient);
        Assert.Equal(hospital.Id, savedPatient.PreferredHospitalId);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsPatient()
    {
        // Arrange
        var password = "TestPassword123!";
        var patient = MockDataBuilder.CreatePatient(
            email: "auth@test.com",
            password: password);
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _patientService.AuthenticateAsync("auth@test.com", password);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(patient.Id, result.Id);
        Assert.Equal("auth@test.com", result.Email);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidPassword_ReturnsNull()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient(
            email: "auth@test.com",
            password: "CorrectPassword123!");
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _patientService.AuthenticateAsync("auth@test.com", "WrongPassword");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithNonExistentEmail_ReturnsNull()
    {
        // Act
        var result = await _patientService.AuthenticateAsync("nonexistent@test.com", "Password123!");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_WithExistingEmail_ReturnsPatient()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient(email: "getbyemail@test.com");
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _patientService.GetByEmailAsync("getbyemail@test.com");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(patient.Id, result.Id);
    }

    [Fact]
    public async Task GetByEmailAsync_WithNonExistentEmail_ReturnsNull()
    {
        // Act
        var result = await _patientService.GetByEmailAsync("nonexistent@test.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsPatient()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _patientService.GetByIdAsync(patient.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(patient.Id, result.Id);
    }

    [Fact]
    public async Task IsEmailTakenAsync_WithExistingEmail_ReturnsTrue()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient(email: "taken@test.com");
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _patientService.IsEmailTakenAsync("taken@test.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsEmailTakenAsync_WithNewEmail_ReturnsFalse()
    {
        // Act
        var result = await _patientService.IsEmailTakenAsync("newemail@test.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesPatient()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        patient.FirstName = "UpdatedFirstName";
        patient.LastName = "UpdatedLastName";

        // Act
        await _patientService.UpdateAsync(patient);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.Patients.FindAsync(patient.Id);
        Assert.NotNull(updated);
        Assert.Equal("UpdatedFirstName", updated.FirstName);
        Assert.Equal("UpdatedLastName", updated.LastName);
    }

    [Fact]
    public async Task GetByHospitalIdsAsync_WithMatchingHospitals_ReturnsPatients()
    {
        // Arrange
        var hospital1 = MockDataBuilder.CreateHospital();
        var hospital2 = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.AddRange(hospital1, hospital2);
        await _dbContext.SaveChangesAsync();

        var patient1 = MockDataBuilder.CreatePatient();
        patient1.PreferredHospitalId = hospital1.Id;
        var patient2 = MockDataBuilder.CreatePatient();
        patient2.PreferredHospitalId = hospital2.Id;
        var patient3 = MockDataBuilder.CreatePatient();
        patient3.PreferredHospitalId = null;

        _dbContext.Patients.AddRange(patient1, patient2, patient3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _patientService.GetByHospitalIdsAsync(new[] { hospital1.Id, hospital2.Id });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Id == patient1.Id);
        Assert.Contains(result, p => p.Id == patient2.Id);
        Assert.DoesNotContain(result, p => p.Id == patient3.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPatients()
    {
        // Arrange
        var patients = new[]
        {
            MockDataBuilder.CreatePatient(),
            MockDataBuilder.CreatePatient(),
            MockDataBuilder.CreatePatient()
        };
        _dbContext.Patients.AddRange(patients);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _patientService.GetAllAsync();

        // Assert
        Assert.True(result.Count >= 3);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingPatient_DeletesPatient()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        _dbContext.Patients.Add(patient);
        _dbContext.MedicalDatas.Add(medicalData);
        await _dbContext.SaveChangesAsync();

        // Act
        await _patientService.DeleteAsync(patient.Id);

        // Assert
        var deleted = await _dbContext.Patients.FindAsync(patient.Id);
        Assert.Null(deleted);
        
        // Verify cascade delete
        var deletedMedicalData = await _dbContext.MedicalDatas.FindAsync(medicalData.Id);
        Assert.Null(deletedMedicalData);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentPatient_DoesNotThrow()
    {
        // Act & Assert
        await _patientService.DeleteAsync(99999);
        // Should not throw
    }

    [Fact]
    public async Task RegisterAsync_WithNullPatient_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _patientService.RegisterAsync(null!, "Password123!"));
    }

    [Fact]
    public async Task RegisterAsync_WithEmptyPassword_ThrowsException()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();

        _hospitalServiceMock.Setup(x => x.FindClosestHospitalAsync(It.IsAny<Domicile>()))
            .ReturnsAsync((Hospital?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _patientService.RegisterAsync(patient, ""));
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyEmail_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _patientService.AuthenticateAsync("", "Password123!"));
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyPassword_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _patientService.AuthenticateAsync("test@test.com", ""));
    }

    [Fact]
    public async Task GetByEmailAsync_WithEmptyEmail_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _patientService.GetByEmailAsync(""));
    }

    [Fact]
    public async Task IsEmailTakenAsync_WithEmptyEmail_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _patientService.IsEmailTakenAsync(""));
    }

    [Fact]
    public async Task UpdateAsync_WithNullPatient_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _patientService.UpdateAsync(null!));
    }

    [Fact]
    public async Task GetByHospitalIdsAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Act
        var result = await _patientService.GetByHospitalIdsAsync(Array.Empty<int>());

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByHospitalIdsAsync_WithNullList_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _patientService.GetByHospitalIdsAsync(null!));
    }

    [Fact]
    public async Task DeleteAsync_WithCascadeDelete_RemovesRelatedData()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var medicalData = MockDataBuilder.CreateMedicalData(patient.Id);
        var issue = MockDataBuilder.CreatePatientIssue(patient.Id);
        _dbContext.MedicalDatas.Add(medicalData);
        _dbContext.PatientIssues.Add(issue);
        await _dbContext.SaveChangesAsync();

        // Act
        await _patientService.DeleteAsync(patient.Id);

        // Assert
        var deletedMedicalData = await _dbContext.MedicalDatas.FindAsync(medicalData.Id);
        Assert.Null(deletedMedicalData);

        var deletedIssue = await _dbContext.PatientIssues.FindAsync(issue.Id);
        Assert.Null(deletedIssue);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

