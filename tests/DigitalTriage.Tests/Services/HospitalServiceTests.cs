using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using DigitalTriage.Infrastructure.Services;
using DigitalTriage.Tests.Helpers;
using DigitalTriage.Tests.TestFixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DigitalTriage.Tests.Services;

public class HospitalServiceTests : IDisposable
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly IHospitalService _hospitalService;

    public HospitalServiceTests()
    {
        var fixture = new DatabaseFixture();
        _dbContext = fixture.CreateDbContext();
        var logger = TestLoggerFactory.Create<HospitalService>();
        _hospitalService = new HospitalService(_dbContext, logger);
    }

    [Fact]
    public async Task CreateHospitalAsync_WithValidData_CreatesHospital()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id, "Cardiology");
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital(
            name: "New Hospital",
            country: "Romania",
            city: "Bucharest");

        // Act
        var result = await _hospitalService.CreateHospitalAsync(doctor.Id, hospital);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("New Hospital", result.Name);
        Assert.Equal(doctorProfile.Id, result.CreatedByDoctorId);
    }

    [Fact]
    public async Task UpdateHospitalAsync_WithValidData_UpdatesHospital()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital();
        hospital.CreatedByDoctorId = doctorProfile.Id;
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        hospital.Name = "Updated Hospital Name";
        hospital.City = "Cluj-Napoca";

        // Act
        await _hospitalService.UpdateHospitalAsync(doctor.Id, hospital);

        // Assert
        var updated = await _dbContext.Hospitals.FindAsync(hospital.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Hospital Name", updated.Name);
        Assert.Equal("Cluj-Napoca", updated.City);
    }

    [Fact]
    public async Task UpdateHospitalAsync_WithNonMemberDoctor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _hospitalService.UpdateHospitalAsync(doctor.Id, hospital));
    }

    [Fact]
    public async Task JoinHospitalAsync_WithValidData_JoinsDoctor()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        // Act
        await _hospitalService.JoinHospitalAsync(doctor.Id, hospital.Id);

        // Assert
        var membership = await _dbContext.DoctorHospitalMemberships
            .FirstOrDefaultAsync(m => m.DoctorId == doctorProfile.Id && m.HospitalId == hospital.Id);
        Assert.NotNull(membership);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task LeaveHospitalAsync_WithActiveMembership_DeactivatesMembership()
    {
        // Arrange
        var doctor1 = MockDataBuilder.CreatePatient(asDoctor: true);
        var doctor2 = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.AddRange(doctor1, doctor2);
        await _dbContext.SaveChangesAsync();

        var doctorProfile1 = MockDataBuilder.CreateDoctorProfile(doctor1.Id);
        var doctorProfile2 = MockDataBuilder.CreateDoctorProfile(doctor2.Id);
        _dbContext.DoctorProfiles.AddRange(doctorProfile1, doctorProfile2);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        var membership1 = MockDataBuilder.CreateMembership(doctorProfile1.Id, hospital.Id, isActive: true);
        var membership2 = MockDataBuilder.CreateMembership(doctorProfile2.Id, hospital.Id, isActive: true);
        _dbContext.DoctorHospitalMemberships.AddRange(membership1, membership2);
        await _dbContext.SaveChangesAsync();

        // Act
        await _hospitalService.LeaveHospitalAsync(doctor1.Id, hospital.Id);

        // Assert
        var updatedMembership = await _dbContext.DoctorHospitalMemberships
            .FirstOrDefaultAsync(m => m.DoctorId == doctorProfile1.Id && m.HospitalId == hospital.Id);
        Assert.NotNull(updatedMembership);
        Assert.False(updatedMembership.IsActive);
        Assert.NotNull(updatedMembership.LeftAt);
        
        // Verify hospital still exists (because doctor2 is still a member)
        var hospitalStillExists = await _dbContext.Hospitals.FindAsync(hospital.Id);
        Assert.NotNull(hospitalStillExists);
    }

    [Fact]
    public async Task LeaveHospitalAsync_WithLastDoctor_DeletesHospital()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id, isActive: true);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Act
        await _hospitalService.LeaveHospitalAsync(doctor.Id, hospital.Id);

        // Assert
        var deletedHospital = await _dbContext.Hospitals.FindAsync(hospital.Id);
        Assert.Null(deletedHospital);
    }

    [Fact]
    public async Task GetHospitalsForDoctorAsync_ReturnsActiveHospitals()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital1 = MockDataBuilder.CreateHospital(name: "Hospital 1");
        var hospital2 = MockDataBuilder.CreateHospital(name: "Hospital 2");
        _dbContext.Hospitals.AddRange(hospital1, hospital2);
        await _dbContext.SaveChangesAsync();

        var activeMembership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital1.Id, isActive: true);
        var inactiveMembership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital2.Id, isActive: false);
        _dbContext.DoctorHospitalMemberships.AddRange(activeMembership, inactiveMembership);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.GetHospitalsForDoctorAsync(doctor.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(hospital1.Id, result[0].Id);
    }

    [Fact]
    public async Task SearchHospitalsAsync_WithNameSearch_ReturnsMatchingHospitals()
    {
        // Arrange
        var hospital1 = MockDataBuilder.CreateHospital(name: "City Hospital");
        var hospital2 = MockDataBuilder.CreateHospital(name: "General Hospital");
        var hospital3 = MockDataBuilder.CreateHospital(name: "City Clinic");
        _dbContext.Hospitals.AddRange(hospital1, hospital2, hospital3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.SearchHospitalsAsync("City");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, h => h.Name.Contains("City"));
    }

    [Fact]
    public async Task SearchHospitalsAsync_WithLocationFilters_ReturnsMatchingHospitals()
    {
        // Arrange
        var hospital1 = MockDataBuilder.CreateHospital(city: "Bucharest", country: "Romania");
        var hospital2 = MockDataBuilder.CreateHospital(city: "Cluj-Napoca", country: "Romania");
        var hospital3 = MockDataBuilder.CreateHospital(city: "Bucharest", country: "Romania");
        _dbContext.Hospitals.AddRange(hospital1, hospital2, hospital3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.SearchHospitalsAsync(
            searchTerm: null,
            country: "Romania",
            city: "Bucharest");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, h => Assert.Equal("Bucharest", h.City));
    }

    [Fact]
    public async Task FindClosestHospitalAsync_WithMatchingDomicile_ReturnsBestMatch()
    {
        // Arrange
        var domicile = MockDataBuilder.CreateDomicile(
            country: "Romania",
            county: "Bucharest",
            city: "Bucharest");

        var hospital1 = MockDataBuilder.CreateHospital(
            name: "Exact Match",
            country: "Romania",
            county: "Bucharest",
            city: "Bucharest");
        var hospital2 = MockDataBuilder.CreateHospital(
            name: "Partial Match",
            country: "Romania",
            city: "Cluj-Napoca");
        _dbContext.Hospitals.AddRange(hospital1, hospital2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.FindClosestHospitalAsync(domicile);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Exact Match", result.Name);
    }

    [Fact]
    public async Task FindClosestHospitalAsync_WithNoHospitals_ReturnsNull()
    {
        // Arrange
        var domicile = MockDataBuilder.CreateDomicile();

        // Act
        var result = await _hospitalService.FindClosestHospitalAsync(domicile);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetPatientPreferredHospitalAsync_WithValidData_SetsPreferredHospital()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Patients.Add(patient);
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        // Act
        await _hospitalService.SetPatientPreferredHospitalAsync(patient.Id, hospital.Id);

        // Assert
        var updated = await _dbContext.Patients.FindAsync(patient.Id);
        Assert.NotNull(updated);
        Assert.Equal(hospital.Id, updated.PreferredHospitalId);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsHospital()
    {
        // Arrange
        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.GetByIdAsync(hospital.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(hospital.Id, result.Id);
    }

    [Fact]
    public async Task GetHospitalWithDoctorsAsync_ReturnsHospitalWithActiveDoctors()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id, isActive: true);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.GetHospitalWithDoctorsAsync(hospital.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.DoctorMemberships);
        Assert.True(result.DoctorMemberships.First().IsActive);
    }

    [Fact]
    public async Task CreateHospitalAsync_WithNullHospital_ThrowsException()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _hospitalService.CreateHospitalAsync(doctor.Id, null!));
    }

    [Fact]
    public async Task UpdateHospitalAsync_WithNullHospital_ThrowsException()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _hospitalService.UpdateHospitalAsync(doctor.Id, null!));
    }

    [Fact]
    public async Task UpdateHospitalAsync_WithNonExistentHospital_ThrowsException()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital();
        hospital.Id = 99999; // Non-existent ID

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _hospitalService.UpdateHospitalAsync(doctor.Id, hospital));
    }

    [Fact]
    public async Task JoinHospitalAsync_WithNonExistentHospital_ThrowsException()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _hospitalService.JoinHospitalAsync(doctor.Id, 99999));
    }

    [Fact]
    public async Task LeaveHospitalAsync_WithNonExistentMembership_ThrowsException()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _hospitalService.LeaveHospitalAsync(doctor.Id, hospital.Id));
    }

    [Fact]
    public async Task SetPatientPreferredHospitalAsync_WithNonExistentPatient_ThrowsException()
    {
        // Arrange
        var hospital = MockDataBuilder.CreateHospital();
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _hospitalService.SetPatientPreferredHospitalAsync(99999, hospital.Id));
    }

    [Fact]
    public async Task SetPatientPreferredHospitalAsync_WithNonExistentHospital_ThrowsException()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _hospitalService.SetPatientPreferredHospitalAsync(patient.Id, 99999));
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _hospitalService.GetByIdAsync(99999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllHospitals()
    {
        // Arrange
        var hospitals = new[]
        {
            MockDataBuilder.CreateHospital(name: "Hospital A"),
            MockDataBuilder.CreateHospital(name: "Hospital B"),
            MockDataBuilder.CreateHospital(name: "Hospital C")
        };
        _dbContext.Hospitals.AddRange(hospitals);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.GetAllAsync();

        // Assert
        Assert.True(result.Count >= 3);
        Assert.Contains(result, h => h.Name == "Hospital A");
        Assert.Contains(result, h => h.Name == "Hospital B");
        Assert.Contains(result, h => h.Name == "Hospital C");
    }

    [Fact]
    public async Task SearchHospitalsAsync_WithMaxResults_LimitsResults()
    {
        // Arrange
        var hospitals = Enumerable.Range(1, 30)
            .Select(i => MockDataBuilder.CreateHospital(name: $"Hospital {i}"))
            .ToArray();
        _dbContext.Hospitals.AddRange(hospitals);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.SearchHospitalsAsync(null, maxResults: 10);

        // Assert
        Assert.True(result.Count <= 10);
    }

    [Fact]
    public async Task GetHospitalsForDoctorAsync_WithNoMemberships_ReturnsEmptyList()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _hospitalService.GetHospitalsForDoctorAsync(doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

