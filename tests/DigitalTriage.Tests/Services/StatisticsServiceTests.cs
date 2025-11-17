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

public class StatisticsServiceTests : IDisposable
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly IStatisticsService _statisticsService;

    public StatisticsServiceTests()
    {
        var fixture = new DatabaseFixture();
        _dbContext = fixture.CreateDbContext();
        var logger = TestLoggerFactory.Create<StatisticsService>();
        _statisticsService = new StatisticsService(_dbContext, logger);
    }

    [Fact]
    public async Task GetFiltersAsync_WithDoctor_ReturnsFilterOptions()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        var hospital = MockDataBuilder.CreateHospital(
            country: "Romania",
            county: "Bucharest",
            city: "Bucharest");
        _dbContext.Hospitals.Add(hospital);
        await _dbContext.SaveChangesAsync();

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _statisticsService.GetFiltersAsync(doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Hospitals);
        Assert.Contains(result.Countries, c => c.Name == "Romania");
    }

    [Fact]
    public async Task GetSummaryAsync_WithActiveIssues_ReturnsSummary()
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

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var patient = MockDataBuilder.CreatePatient();
        patient.PreferredHospitalId = hospital.Id;
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var issue1 = MockDataBuilder.CreatePatientIssue(
            patient.Id,
            problemType: "Cardiac",
            emergencyGrade: EsiLevel.Critical,
            isActive: true);
        var issue2 = MockDataBuilder.CreatePatientIssue(
            patient.Id,
            problemType: "Respiratory",
            emergencyGrade: EsiLevel.Urgent,
            isActive: true);
        _dbContext.PatientIssues.AddRange(issue1, issue2);
        await _dbContext.SaveChangesAsync();

        var query = new StatisticsQueryDto();

        // Act
        var result = await _statisticsService.GetSummaryAsync(query, doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalActiveIssues);
        Assert.Equal(1, result.UniquePatientsWithActiveIssues);
        Assert.Equal(1, result.HospitalsIncluded);
        Assert.True(result.DistributionByType.ContainsKey("Cardiac"));
        Assert.True(result.DistributionByType.ContainsKey("Respiratory"));
    }

    [Fact]
    public async Task GetSummaryAsync_WithHospitalFilter_ReturnsFilteredSummary()
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

        var membership1 = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital1.Id);
        var membership2 = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital2.Id);
        _dbContext.DoctorHospitalMemberships.AddRange(membership1, membership2);
        await _dbContext.SaveChangesAsync();

        var patient1 = MockDataBuilder.CreatePatient();
        patient1.PreferredHospitalId = hospital1.Id;
        var patient2 = MockDataBuilder.CreatePatient();
        patient2.PreferredHospitalId = hospital2.Id;
        _dbContext.Patients.AddRange(patient1, patient2);
        await _dbContext.SaveChangesAsync();

        var issue1 = MockDataBuilder.CreatePatientIssue(patient1.Id, isActive: true);
        var issue2 = MockDataBuilder.CreatePatientIssue(patient2.Id, isActive: true);
        _dbContext.PatientIssues.AddRange(issue1, issue2);
        await _dbContext.SaveChangesAsync();

        var query = new StatisticsQueryDto { HospitalId = hospital1.Id };

        // Act
        var result = await _statisticsService.GetSummaryAsync(query, doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalActiveIssues);
    }

    [Fact]
    public async Task GetSummaryAsync_WithDateRangeFilter_ReturnsFilteredSummary()
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

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var patient = MockDataBuilder.CreatePatient();
        patient.PreferredHospitalId = hospital.Id;
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var issue1 = MockDataBuilder.CreatePatientIssue(patient.Id, isActive: true);
        issue1.LastUpdateDate = DateTimeOffset.UtcNow.AddDays(-5);
        var issue2 = MockDataBuilder.CreatePatientIssue(patient.Id, isActive: true);
        issue2.LastUpdateDate = DateTimeOffset.UtcNow.AddDays(-10);
        _dbContext.PatientIssues.AddRange(issue1, issue2);
        await _dbContext.SaveChangesAsync();

        var query = new StatisticsQueryDto
        {
            FromDate = DateTime.UtcNow.AddDays(-7),
            ToDate = DateTime.UtcNow
        };

        // Act
        var result = await _statisticsService.GetSummaryAsync(query, doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalActiveIssues); // Only issue1 is within range
    }

    [Fact]
    public async Task GetSummaryAsync_WithProblemTypeFilter_ReturnsFilteredSummary()
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

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var patient = MockDataBuilder.CreatePatient();
        patient.PreferredHospitalId = hospital.Id;
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var issue1 = MockDataBuilder.CreatePatientIssue(patient.Id, problemType: "Cardiac", isActive: true);
        var issue2 = MockDataBuilder.CreatePatientIssue(patient.Id, problemType: "Respiratory", isActive: true);
        _dbContext.PatientIssues.AddRange(issue1, issue2);
        await _dbContext.SaveChangesAsync();

        var query = new StatisticsQueryDto
        {
            ProblemTypes = new List<string> { "Cardiac" }
        };

        // Act
        var result = await _statisticsService.GetSummaryAsync(query, doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalActiveIssues);
        Assert.True(result.DistributionByType.ContainsKey("Cardiac"));
        Assert.False(result.DistributionByType.ContainsKey("Respiratory"));
    }

    [Fact]
    public async Task ExportAsync_WithJsonFormat_ReturnsJsonExport()
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

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var query = new StatisticsQueryDto();

        // Act
        var result = await _statisticsService.ExportAsync(query, "json", doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/json", result.ContentType);
        Assert.Contains(".json", result.FileName);
        Assert.NotNull(result.Content);
    }

    [Fact]
    public async Task ExportAsync_WithCsvFormat_ReturnsCsvExport()
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

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var query = new StatisticsQueryDto();

        // Act
        var result = await _statisticsService.ExportAsync(query, "csv", doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("text/csv", result.ContentType);
        Assert.Contains(".csv", result.FileName);
        Assert.NotNull(result.Content);
        Assert.Contains("Total Active Issues", result.Content);
    }

    [Fact]
    public async Task GetSummaryAsync_WithNoIssues_ReturnsZeroCounts()
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

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var query = new StatisticsQueryDto();

        // Act
        var result = await _statisticsService.GetSummaryAsync(query, doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalActiveIssues);
        Assert.Equal(0, result.UniquePatientsWithActiveIssues);
    }

    [Fact]
    public async Task GetSummaryAsync_WithInactiveIssues_ExcludesInactiveIssues()
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

        var membership = MockDataBuilder.CreateMembership(doctorProfile.Id, hospital.Id);
        _dbContext.DoctorHospitalMemberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        var patient = MockDataBuilder.CreatePatient();
        patient.PreferredHospitalId = hospital.Id;
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var activeIssue = MockDataBuilder.CreatePatientIssue(patient.Id, isActive: true);
        var inactiveIssue = MockDataBuilder.CreatePatientIssue(patient.Id, isActive: false);
        _dbContext.PatientIssues.AddRange(activeIssue, inactiveIssue);
        await _dbContext.SaveChangesAsync();

        var query = new StatisticsQueryDto();

        // Act
        var result = await _statisticsService.GetSummaryAsync(query, doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalActiveIssues);
    }

    [Fact]
    public async Task GetFiltersAsync_WithNoHospitals_ReturnsEmptyHospitalList()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(asDoctor: true);
        _dbContext.Patients.Add(doctor);
        await _dbContext.SaveChangesAsync();

        var doctorProfile = MockDataBuilder.CreateDoctorProfile(doctor.Id);
        _dbContext.DoctorProfiles.Add(doctorProfile);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _statisticsService.GetFiltersAsync(doctor.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Hospitals);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

