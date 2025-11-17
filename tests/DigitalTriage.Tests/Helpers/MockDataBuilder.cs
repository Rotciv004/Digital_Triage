using BCrypt.Net;
using DigitalTriage.Domain.Entities;

namespace DigitalTriage.Tests.Helpers;

/// <summary>
/// Builder class for creating mock data entities for testing.
/// </summary>
public static class MockDataBuilder
{
    private static int _patientCounter = 1;
    private static int _hospitalCounter = 1;
    private static int _medicalDataCounter = 1;
    private static int _issueCounter = 1;

    public static Patient CreatePatient(
        string? email = null,
        string? password = null,
        string? firstName = null,
        string? lastName = null,
        string? role = "Patient",
        bool withDomicile = false,
        bool withPlaceOfBirth = false,
        bool asDoctor = false)
    {
        var patientId = _patientCounter++;
        var patientEmail = email ?? $"patient{patientId}@test.com";
        var patientPassword = password ?? "TestPassword123!";

        var patient = new Patient
        {
            Id = patientId,
            Email = patientEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(patientPassword),
            FirstName = firstName ?? $"FirstName{patientId}",
            LastName = lastName ?? $"LastName{patientId}",
            PhoneNumber = $"071234567{patientId}",
            Cnp = $"123456789012{patientId % 10}",
            Serie = "AB",
            Nr = $"{patientId:D6}",
            Citizenship = "Romanian",
            Role = role
        };

        if (withPlaceOfBirth)
        {
            patient.PlaceOfBirth = new PlaceOfBirth
            {
                Id = patientId,
                Country = "Romania",
                County = "Bucharest",
                City = "Bucharest"
            };
            patient.PlaceOfBirthId = patientId;
        }

        if (withDomicile)
        {
            patient.Domicile = new Domicile
            {
                Id = patientId,
                Country = "Romania",
                County = "Bucharest",
                City = "Bucharest",
                Street = $"Test Street {patientId}",
                Number = $"{patientId}"
            };
            patient.DomicileId = patientId;
        }

        if (asDoctor)
        {
            patient.DoctorProfile = new DoctorProfile
            {
                UserId = patientId,
                Specialization = "Cardiology"
            };
            patient.Role = "Doctor";
        }

        return patient;
    }

    public static Hospital CreateHospital(
        string? name = null,
        string? country = "Romania",
        string? county = "Bucharest",
        string? city = "Bucharest",
        string? street = null,
        string? number = null,
        int? createdByDoctorId = null)
    {
        var hospitalId = _hospitalCounter++;
        return new Hospital
        {
            Id = hospitalId,
            Name = name ?? $"Test Hospital {hospitalId}",
            Country = country,
            County = county,
            City = city,
            Street = street ?? $"Hospital Street {hospitalId}",
            Number = number ?? $"{hospitalId}",
            CreatedByDoctorId = createdByDoctorId
        };
    }

    public static MedicalData CreateMedicalData(
        int patientId,
        string? bloodType = "O+",
        string? allergies = "None",
        string? symptoms = "Fever, Headache",
        bool isConfidential = true,
        int? authorizedDoctorId = null)
    {
        var medicalDataId = _medicalDataCounter++;
        return new MedicalData
        {
            Id = medicalDataId,
            PatientId = patientId,
            BloodType = bloodType,
            Allergies = allergies,
            Symptoms = symptoms,
            IsConfidential = isConfidential,
            AuthorizedDoctorId = authorizedDoctorId,
            CreatedAt = DateTime.UtcNow,
            LastVisitDate = DateTime.UtcNow
        };
    }

    public static PatientIssue CreatePatientIssue(
        int patientId,
        string? title = null,
        string? description = null,
        string? problemType = "Cardiac",
        EsiLevel? emergencyGrade = EsiLevel.Urgent,
        bool isActive = true)
    {
        var issueId = _issueCounter++;
        return new PatientIssue
        {
            Id = issueId,
            PatientId = patientId,
            Title = title ?? $"Test Issue {issueId}",
            Description = description ?? $"Test Description {issueId}",
            ProblemType = problemType,
            EmergencyGrade = emergencyGrade,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdateDate = DateTimeOffset.UtcNow
        };
    }

    public static DoctorProfile CreateDoctorProfile(
        int userId,
        string specialization = "Cardiology")
    {
        return new DoctorProfile
        {
            Id = userId,
            UserId = userId,
            Specialization = specialization
        };
    }

    public static DoctorHospitalMembership CreateMembership(
        int doctorId,
        int hospitalId,
        bool isActive = true,
        DateTime? joinedAt = null,
        DateTime? leftAt = null)
    {
        return new DoctorHospitalMembership
        {
            Id = doctorId * 1000 + hospitalId, // Simple unique ID generation
            DoctorId = doctorId,
            HospitalId = hospitalId,
            IsActive = isActive,
            JoinedAt = joinedAt ?? DateTime.UtcNow,
            LeftAt = leftAt
        };
    }

    public static MedicalFile CreateMedicalFile(
        int medicalDataId,
        string? fileName = null,
        string? filePath = null)
    {
        var fileId = _medicalDataCounter++;
        return new MedicalFile
        {
            Id = fileId,
            MedicalDataId = medicalDataId,
            FileName = fileName ?? $"test_file_{fileId}.pdf",
            FilePath = filePath ?? $"uploads/test_file_{fileId}.pdf",
            UploadDate = DateTime.UtcNow
        };
    }

    public static PlaceOfBirth CreatePlaceOfBirth(
        string? country = "Romania",
        string? county = "Bucharest",
        string? city = "Bucharest")
    {
        return new PlaceOfBirth
        {
            Id = _patientCounter++,
            Country = country,
            County = county,
            City = city
        };
    }

    public static Domicile CreateDomicile(
        string? country = "Romania",
        string? county = "Bucharest",
        string? city = "Bucharest",
        string? street = "Test Street",
        string? number = "1")
    {
        return new Domicile
        {
            Id = _patientCounter++,
            Country = country,
            County = county,
            City = city,
            Street = street,
            Number = number
        };
    }

    /// <summary>
    /// Resets the counters (useful for test cleanup).
    /// </summary>
    public static void ResetCounters()
    {
        _patientCounter = 1;
        _hospitalCounter = 1;
        _medicalDataCounter = 1;
        _issueCounter = 1;
    }
}

