using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using DigitalTriage.Infrastructure.Services;
using DigitalTriage.Tests.Helpers;
using DigitalTriage.Tests.TestFixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DigitalTriage.Tests.Services;

public class FamilyMedicServiceTests : IDisposable
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IPatientService> _patientServiceMock;
    private readonly IFamilyMedicService _familyMedicService;
    private readonly IConfiguration _configuration;

    public FamilyMedicServiceTests()
    {
        var fixture = new DatabaseFixture();
        _dbContext = fixture.CreateDbContext();
        
        _emailServiceMock = new Mock<IEmailService>();
        _patientServiceMock = new Mock<IPatientService>();
        
        // Setup configuration for encryption key
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Encryption:Key", "TestEncryptionKey32Bytes!!!" }
        });
        _configuration = configBuilder.Build();

        var logger = TestLoggerFactory.Create<FamilyMedicService>();
        _familyMedicService = new FamilyMedicService(
            _dbContext,
            logger,
            _emailServiceMock.Object,
            _patientServiceMock.Object,
            _configuration);
    }

    [Fact]
    public async Task RequestFamilyMedicAsync_WithValidData_CreatesRequest()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient(email: "patient@test.com");
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        _emailServiceMock.Setup(x => x.SendFamilyMedicRequestNotificationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _familyMedicService.RequestFamilyMedicAsync(patient.Id, doctor.Email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(patient.Id, result.PatientId);
        Assert.Equal(doctor.Email, result.DoctorEmail);
        
        var savedRequest = await _dbContext.FamilyMedicRequests.FindAsync(result.Id);
        Assert.NotNull(savedRequest);
        Assert.Equal("Pending", savedRequest.Status);
    }

    [Fact]
    public async Task RequestFamilyMedicAsync_WithNonExistentPatient_ThrowsException()
    {
        // Arrange
        _patientServiceMock.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Patient?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _familyMedicService.RequestFamilyMedicAsync(999, "doctor@test.com"));
    }

    [Fact]
    public async Task RequestFamilyMedicAsync_WithNonExistentDoctor_ThrowsException()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _familyMedicService.RequestFamilyMedicAsync(patient.Id, "nonexistent@test.com"));
    }

    [Fact]
    public async Task RequestFamilyMedicAsync_WithExistingPendingRequest_ThrowsException()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        var existingRequest = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = doctor.Email,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        _dbContext.FamilyMedicRequests.Add(existingRequest);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _familyMedicService.RequestFamilyMedicAsync(patient.Id, doctor.Email));
    }

    [Fact]
    public async Task RequestFamilyMedicAsync_WithExistingFamilyMedic_RemovesOldAssignment()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var oldDoctor = MockDataBuilder.CreatePatient(email: "olddoctor@test.com", asDoctor: true);
        var newDoctor = MockDataBuilder.CreatePatient(email: "newdoctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, oldDoctor, newDoctor);
        await _dbContext.SaveChangesAsync();

        patient.FamilyMedicEmail = oldDoctor.Email;
        patient.FamilyMedicDoctorId = oldDoctor.DoctorProfile!.Id;
        _dbContext.Patients.Update(patient);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        _emailServiceMock.Setup(x => x.SendFamilyMedicRequestNotificationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _familyMedicService.RequestFamilyMedicAsync(patient.Id, newDoctor.Email);

        // Assert
        var updatedPatient = await _dbContext.Patients.FindAsync(patient.Id);
        Assert.NotNull(updatedPatient);
        Assert.Null(updatedPatient.FamilyMedicEmail);
        Assert.Null(updatedPatient.FamilyMedicDoctorId);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task AcceptFamilyMedicRequestAsync_WithValidRequest_AcceptsRequest()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        var request = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = doctor.Email,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        _dbContext.FamilyMedicRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.AcceptFamilyMedicRequestAsync(request.Id, doctor.DoctorProfile!.Id);

        // Assert
        Assert.True(result);
        
        var updatedRequest = await _dbContext.FamilyMedicRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == request.Id);
        Assert.NotNull(updatedRequest);
        Assert.Equal("Accepted", updatedRequest.Status);
        Assert.NotNull(updatedRequest.RespondedAt);
        Assert.Equal(doctor.DoctorProfile.Id, updatedRequest.DoctorId);
        
        var updatedPatient = await _dbContext.Patients.FindAsync(patient.Id);
        Assert.NotNull(updatedPatient);
        Assert.Equal(doctor.Email, updatedPatient.FamilyMedicEmail);
        Assert.Equal(doctor.DoctorProfile.Id, updatedPatient.FamilyMedicDoctorId);
    }

    [Fact]
    public async Task AcceptFamilyMedicRequestAsync_WithNonExistentRequest_ReturnsFalse()
    {
        // Act
        var result = await _familyMedicService.AcceptFamilyMedicRequestAsync(999, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AcceptFamilyMedicRequestAsync_WithNonPendingRequest_ReturnsFalse()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        var request = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = doctor.Email,
            Status = "Rejected",
            RequestedAt = DateTime.UtcNow
        };
        _dbContext.FamilyMedicRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.AcceptFamilyMedicRequestAsync(request.Id, doctor.DoctorProfile!.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AcceptFamilyMedicRequestAsync_RejectsOtherPendingRequests()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor1 = MockDataBuilder.CreatePatient(email: "doctor1@test.com", asDoctor: true);
        var doctor2 = MockDataBuilder.CreatePatient(email: "doctor2@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor1, doctor2);
        await _dbContext.SaveChangesAsync();

        var request1 = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = doctor1.Email,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        var request2 = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = doctor2.Email,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow.AddMinutes(1)
        };
        _dbContext.FamilyMedicRequests.AddRange(request1, request2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.AcceptFamilyMedicRequestAsync(request1.Id, doctor1.DoctorProfile!.Id);

        // Assert
        Assert.True(result);
        
        var updatedRequest2 = await _dbContext.FamilyMedicRequests.FindAsync(request2.Id);
        Assert.NotNull(updatedRequest2);
        Assert.Equal("Rejected", updatedRequest2.Status);
    }

    [Fact]
    public async Task RejectFamilyMedicRequestAsync_WithValidRequest_RejectsRequest()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        var request = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = doctor.Email,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        _dbContext.FamilyMedicRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.RejectFamilyMedicRequestAsync(request.Id, doctor.DoctorProfile!.Id);

        // Assert
        Assert.True(result);
        
        var updatedRequest = await _dbContext.FamilyMedicRequests.FindAsync(request.Id);
        Assert.NotNull(updatedRequest);
        Assert.Equal("Rejected", updatedRequest.Status);
        Assert.NotNull(updatedRequest.RespondedAt);
    }

    [Fact]
    public async Task RejectFamilyMedicRequestAsync_WithNonExistentRequest_ReturnsFalse()
    {
        // Act
        var result = await _familyMedicService.RejectFamilyMedicRequestAsync(999, 1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetPendingRequestsForDoctorAsync_ReturnsOnlyPendingRequests()
    {
        // Arrange
        var patient1 = MockDataBuilder.CreatePatient(email: "patient1@test.com");
        var patient2 = MockDataBuilder.CreatePatient(email: "patient2@test.com");
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient1, patient2, doctor);
        await _dbContext.SaveChangesAsync();

        var pendingRequest = new FamilyMedicRequest
        {
            PatientId = patient1.Id,
            DoctorEmail = doctor.Email,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        var acceptedRequest = new FamilyMedicRequest
        {
            PatientId = patient2.Id,
            DoctorEmail = doctor.Email,
            Status = "Accepted",
            RequestedAt = DateTime.UtcNow
        };
        _dbContext.FamilyMedicRequests.AddRange(pendingRequest, acceptedRequest);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.GetPendingRequestsForDoctorAsync(doctor.Email);

        // Assert
        var requests = result.ToList();
        Assert.Single(requests);
        Assert.Equal(pendingRequest.Id, requests[0].Id);
        Assert.Equal("Pending", requests[0].Status);
    }

    [Fact]
    public async Task GetFamilyPatientsAsync_ReturnsOnlyAssignedPatients()
    {
        // Arrange
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        var patient1 = MockDataBuilder.CreatePatient(email: "patient1@test.com");
        var patient2 = MockDataBuilder.CreatePatient(email: "patient2@test.com");
        var patient3 = MockDataBuilder.CreatePatient(email: "patient3@test.com");
        _dbContext.Patients.AddRange(doctor, patient1, patient2, patient3);
        await _dbContext.SaveChangesAsync();

        patient1.FamilyMedicDoctorId = doctor.DoctorProfile!.Id;
        patient2.FamilyMedicDoctorId = doctor.DoctorProfile.Id;
        // patient3 has no family medic
        _dbContext.Patients.UpdateRange(patient1, patient2, patient3);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.GetFamilyPatientsAsync(doctor.DoctorProfile.Id);

        // Assert
        var patients = result.ToList();
        Assert.Equal(2, patients.Count);
        Assert.Contains(patients, p => p.Id == patient1.Id);
        Assert.Contains(patients, p => p.Id == patient2.Id);
        Assert.DoesNotContain(patients, p => p.Id == patient3.Id);
    }

    [Fact]
    public async Task GetCurrentRequestForPatientAsync_ReturnsLatestRequest()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        var oldRequest = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = doctor.Email,
            Status = "Rejected",
            RequestedAt = DateTime.UtcNow.AddDays(-1)
        };
        var newRequest = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = doctor.Email,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };
        _dbContext.FamilyMedicRequests.AddRange(oldRequest, newRequest);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.GetCurrentRequestForPatientAsync(patient.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newRequest.Id, result.Id);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task RemoveFamilyMedicAsync_WithAssignedFamilyMedic_RemovesAssignment()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var doctor = MockDataBuilder.CreatePatient(email: "doctor@test.com", asDoctor: true);
        _dbContext.Patients.AddRange(patient, doctor);
        await _dbContext.SaveChangesAsync();

        patient.FamilyMedicEmail = doctor.Email;
        patient.FamilyMedicDoctorId = doctor.DoctorProfile!.Id;
        _dbContext.Patients.Update(patient);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        // Act
        var result = await _familyMedicService.RemoveFamilyMedicAsync(patient.Id);

        // Assert
        Assert.True(result);
        
        var updatedPatient = await _dbContext.Patients.FindAsync(patient.Id);
        Assert.NotNull(updatedPatient);
        Assert.Null(updatedPatient.FamilyMedicEmail);
        Assert.Null(updatedPatient.FamilyMedicDoctorId);
    }

    [Fact]
    public async Task RemoveFamilyMedicAsync_WithNonExistentPatient_ReturnsFalse()
    {
        // Arrange
        _patientServiceMock.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Patient?)null);

        // Act
        var result = await _familyMedicService.RemoveFamilyMedicAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StoreOutlookTokenAsync_StoresAndEncryptsToken()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        var refreshToken = "test_refresh_token_12345";
        var expiresAt = DateTimeOffset.UtcNow.AddDays(90);

        // Act
        var result = await _familyMedicService.StoreOutlookTokenAsync(patient.Id, refreshToken, expiresAt);

        // Assert
        Assert.True(result);
        
        var updatedRequest = await _dbContext.FamilyMedicRequests
            .FirstOrDefaultAsync(r => r.PatientId == patient.Id);
        Assert.NotNull(updatedRequest);
        Assert.NotNull(updatedRequest.OutlookToken);
        // Token should be encrypted (not equal to original)
        Assert.NotEqual(refreshToken, updatedRequest.OutlookToken);
        Assert.Equal(expiresAt.UtcDateTime, updatedRequest.TokenExpiresAt);
    }

    [Fact]
    public async Task StoreOutlookTokenAsync_WithExistingRequest_UpdatesToken()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var existingRequest = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = "doctor@test.com",
            Status = "Accepted",
            RequestedAt = DateTime.UtcNow,
            OutlookToken = "old_encrypted_token"
        };
        _dbContext.FamilyMedicRequests.Add(existingRequest);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        var newRefreshToken = "new_refresh_token_67890";
        var expiresAt = DateTimeOffset.UtcNow.AddDays(90);

        // Act
        var result = await _familyMedicService.StoreOutlookTokenAsync(patient.Id, newRefreshToken, expiresAt);

        // Assert
        Assert.True(result);
        
        var updatedRequest = await _dbContext.FamilyMedicRequests
            .FirstOrDefaultAsync(r => r.PatientId == patient.Id);
        Assert.NotNull(updatedRequest);
        Assert.NotNull(updatedRequest.OutlookToken);
        // Token should be updated and encrypted
        Assert.NotEqual("old_encrypted_token", updatedRequest.OutlookToken);
        Assert.NotEqual(newRefreshToken, updatedRequest.OutlookToken); // Should be encrypted
    }

    [Fact]
    public async Task StoreOutlookTokenAsync_WithEmptyToken_RemovesToken()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var existingRequest = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = "doctor@test.com",
            Status = "Accepted",
            RequestedAt = DateTime.UtcNow,
            OutlookToken = "encrypted_token",
            TokenExpiresAt = DateTime.UtcNow.AddDays(90)
        };
        _dbContext.FamilyMedicRequests.Add(existingRequest);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.StoreOutlookTokenAsync(patient.Id, string.Empty, DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result);
        
        var updatedRequest = await _dbContext.FamilyMedicRequests
            .FirstOrDefaultAsync(r => r.PatientId == patient.Id);
        Assert.NotNull(updatedRequest);
        Assert.Null(updatedRequest.OutlookToken);
        Assert.Null(updatedRequest.TokenExpiresAt);
    }

    [Fact]
    public async Task GetOutlookTokenAsync_ReturnsDecryptedToken()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        var originalToken = "test_refresh_token_for_decryption";
        var expiresAt = DateTimeOffset.UtcNow.AddDays(90);

        // Store token (will be encrypted)
        await _familyMedicService.StoreOutlookTokenAsync(patient.Id, originalToken, expiresAt);

        // Act
        var result = await _familyMedicService.GetOutlookTokenAsync(patient.Id);

        // Assert
        Assert.NotNull(result);
        // Should return the decrypted original token
        Assert.Equal(originalToken, result);
    }

    [Fact]
    public async Task GetOutlookTokenAsync_WithNoToken_ReturnsNull()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.GetOutlookTokenAsync(patient.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOutlookTokenAsync_WithInvalidEncryptedToken_ReturnsNull()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var request = new FamilyMedicRequest
        {
            PatientId = patient.Id,
            DoctorEmail = "doctor@test.com",
            Status = "Accepted",
            RequestedAt = DateTime.UtcNow,
            OutlookToken = "invalid_encrypted_token_that_cannot_be_decrypted"
        };
        _dbContext.FamilyMedicRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _familyMedicService.GetOutlookTokenAsync(patient.Id);

        // Assert
        // Should return null if decryption fails
        Assert.Null(result);
    }

    [Fact]
    public async Task RequestFamilyMedicAsync_WithEmptyEmail_ThrowsException()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        _patientServiceMock.Setup(x => x.GetByIdAsync(patient.Id))
            .ReturnsAsync(patient);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _familyMedicService.RequestFamilyMedicAsync(patient.Id, ""));
    }

    [Fact]
    public async Task GetPendingRequestsForDoctorAsync_WithEmptyEmail_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _familyMedicService.GetPendingRequestsForDoctorAsync(""));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

