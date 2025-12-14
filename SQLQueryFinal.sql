USE master;
GO

-- Drop database if exists (WARNING: This will delete all data!)
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'MedicalTriageDB')
BEGIN
    ALTER DATABASE MedicalTriageDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE MedicalTriageDB;
END
GO


/*
USE master;
ALTER DATABASE MedicalTriageDb
SET SINGLE_USER
WITH ROLLBACK IMMEDIATE;


DROP DATABASE MedicalTriageDB;
*/



-- Create the database
CREATE DATABASE MedicalTriageDB;
GO

USE MedicalTriageDB;
GO

-- ============================================================================
-- BASE TABLES (Location Data)
-- ============================================================================

CREATE TABLE PlacesOfBirth
(
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    Country  NVARCHAR(100) NULL,
    County   NVARCHAR(100) NULL,
    City     NVARCHAR(100) NULL
);

CREATE TABLE Domiciles
(
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    Country  NVARCHAR(100) NULL,
    County   NVARCHAR(100) NULL,
    City     NVARCHAR(100) NULL,
    Street   NVARCHAR(150) NULL,
    Number   NVARCHAR(20)  NULL
);

-- ============================================================================
-- HOSPITALS TABLE (Must be created before Patients to avoid FK issues)
-- ============================================================================

CREATE TABLE Hospitals
(
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    Name               NVARCHAR(200) NOT NULL,
    Country            NVARCHAR(100) NULL,
    County             NVARCHAR(100) NULL,
    City               NVARCHAR(100) NULL,
    Street             NVARCHAR(150) NULL,
    Number             NVARCHAR(20)  NULL,
    CreatedByDoctorId  INT NULL  -- Will add FK later
);

-- ============================================================================
-- PATIENTS TABLE
-- ============================================================================

CREATE TABLE Patients
(
    Id                   INT IDENTITY(1,1) PRIMARY KEY,
    Email                NVARCHAR(200) NOT NULL,
    PasswordHash         NVARCHAR(MAX) NOT NULL,
    PhoneNumber          NVARCHAR(20) NULL,
    FirstName            NVARCHAR(100) NULL,
    LastName             NVARCHAR(100) NULL,
    Cnp                  NVARCHAR(13) NULL,
    Serie                NVARCHAR(2)  NULL,
    Nr                   NVARCHAR(6)  NULL,
    Citizenship          NVARCHAR(100) NULL,
    PlaceOfBirthId       INT NULL,
    DomicileId           INT NULL,
    Role                 NVARCHAR(20) NULL,
    PreferredHospitalId  INT NULL,
    FamilyMedicEmail     NVARCHAR(200) NULL,
    FamilyMedicDoctorId  INT NULL,  -- Will add FK later
    CONSTRAINT FK_Patients_PlaceOfBirth FOREIGN KEY (PlaceOfBirthId) 
        REFERENCES PlacesOfBirth(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Patients_Domicile FOREIGN KEY (DomicileId) 
        REFERENCES Domiciles(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Patients_PreferredHospital FOREIGN KEY (PreferredHospitalId) 
        REFERENCES Hospitals(Id) ON DELETE SET NULL
);

-- ============================================================================
-- DOCTOR PROFILES TABLE
-- ============================================================================

CREATE TABLE DoctorProfiles
(
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT NOT NULL,
    Specialization NVARCHAR(150) NOT NULL,
    CONSTRAINT FK_DoctorProfiles_Patients FOREIGN KEY (UserId) 
        REFERENCES Patients(Id) ON DELETE CASCADE
);

-- Add FamilyMedicDoctor FK to Patients (now that DoctorProfiles exists)
ALTER TABLE Patients 
    ADD CONSTRAINT FK_Patients_FamilyMedicDoctor FOREIGN KEY (FamilyMedicDoctorId) 
        REFERENCES DoctorProfiles(Id) ON DELETE NO ACTION;

-- Add CreatedByDoctor FK to Hospitals (now that DoctorProfiles exists)
ALTER TABLE Hospitals 
    ADD CONSTRAINT FK_Hospitals_DoctorProfiles FOREIGN KEY (CreatedByDoctorId) 
        REFERENCES DoctorProfiles(Id) ON DELETE SET NULL;

-- ============================================================================
-- DOCTOR-HOSPITAL MEMBERSHIP TABLE
-- ============================================================================

CREATE TABLE DoctorHospitalMemberships
(
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    DoctorId    INT NOT NULL,
    HospitalId  INT NOT NULL,
    JoinedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LeftAt      DATETIME2 NULL,
    IsActive    BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_DoctorHospitalMemberships_Doctors FOREIGN KEY (DoctorId) 
        REFERENCES DoctorProfiles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DoctorHospitalMemberships_Hospitals FOREIGN KEY (HospitalId) 
        REFERENCES Hospitals(Id) ON DELETE CASCADE
);

-- ============================================================================
-- MEDICAL DATA TABLES
-- ============================================================================

CREATE TABLE MedicalDatas
(
    Id                        INT IDENTITY(1,1) PRIMARY KEY,
    BloodType                 NVARCHAR(5) NULL,
    Allergies                 NVARCHAR(1000) NULL,
    ChronicDiseases           NVARCHAR(1000) NULL,
    CurrentMedication         NVARCHAR(1000) NULL,
    PersonalHistory           NVARCHAR(1000) NULL,
    FamilyHistory             NVARCHAR(1000) NULL,
    LivingConditions          NVARCHAR(500) NULL,
    IncidentLocation          NVARCHAR(200) NULL,
    Symptoms                  NVARCHAR(2000) NULL,
    PreliminaryDiagnosis      NVARCHAR(2000) NULL,
    EmergencyContactName      NVARCHAR(200) NULL,
    EmergencyContactPhone     NVARCHAR(20) NULL,
    LastVisitDate             DATETIME2 NULL,
    TriageCategory            NVARCHAR(100) NULL,
    TriageLevel               INT NULL,
    EstimatedWaitTimeMinutes  INT NULL,
    IsConfidential            BIT NOT NULL DEFAULT 1,
    AuthorizedDoctorId        INT NULL,
    CreatedAt                 DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt                 DATETIME2 NULL,
    PatientId                 INT NOT NULL,
    CONSTRAINT FK_MedicalDatas_Patients FOREIGN KEY (PatientId) 
        REFERENCES Patients(Id) ON DELETE CASCADE,
    -- ✅ FIX: Changed to NO ACTION to avoid cascade path conflict
    CONSTRAINT FK_MedicalDatas_DoctorProfiles FOREIGN KEY (AuthorizedDoctorId) 
        REFERENCES DoctorProfiles(Id) ON DELETE NO ACTION
);

CREATE TABLE MedicalFiles
(
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    MedicalDataId  INT NOT NULL,
    FileName       NVARCHAR(255) NOT NULL,
    FilePath       NVARCHAR(255) NOT NULL,
    UploadDate     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_MedicalFiles_MedicalDatas FOREIGN KEY (MedicalDataId) 
        REFERENCES MedicalDatas(Id) ON DELETE CASCADE
);

-- ============================================================================
-- PATIENT ISSUES & FAMILY MEDIC TABLES
-- ============================================================================

CREATE TABLE PatientIssues
(
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    PatientId       INT NOT NULL,
    Title           NVARCHAR(200) NULL,
    Description     NVARCHAR(2000) NULL,
    ProblemType     NVARCHAR(100) NULL,
    EmergencyGrade  INT NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    LastUpdateDate  DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_PatientIssues_Patients FOREIGN KEY (PatientId) 
        REFERENCES Patients(Id) ON DELETE CASCADE
);

CREATE TABLE FamilyMedicRequests
(
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    PatientId       INT NOT NULL,
    DoctorEmail     NVARCHAR(200) NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    RequestedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    RespondedAt     DATETIME2 NULL,
    DoctorId        INT NULL,
    OutlookToken    NVARCHAR(MAX) NULL,
    TokenExpiresAt  DATETIME2 NULL,
    CONSTRAINT FK_FamilyMedicRequests_Patients FOREIGN KEY (PatientId) 
        REFERENCES Patients(Id) ON DELETE CASCADE,
    CONSTRAINT FK_FamilyMedicRequests_DoctorProfiles FOREIGN KEY (DoctorId) 
        REFERENCES DoctorProfiles(Id) ON DELETE NO ACTION
);

-- ============================================================================
-- AI TRIAGE TABLES (NEW)
-- ============================================================================

CREATE TABLE TriageSessions
(
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    PatientId          INT NOT NULL,
    StartedAt          DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    EndedAt            DATETIMEOFFSET NULL,
    IsActive           BIT NOT NULL DEFAULT 1,
    Severity           NVARCHAR(20) NULL,
    RecommendedAction  NVARCHAR(500) NULL,
    IsEmergency        BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_TriageSessions_Patients FOREIGN KEY (PatientId) 
        REFERENCES Patients(Id) ON DELETE CASCADE
);

CREATE TABLE TriageMessages
(
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    SessionId  INT NOT NULL,
    Role       NVARCHAR(20) NOT NULL,
    Content    NVARCHAR(MAX) NOT NULL,
    CreatedAt  DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_TriageMessages_TriageSessions FOREIGN KEY (SessionId) 
        REFERENCES TriageSessions(Id) ON DELETE CASCADE
);

CREATE TABLE AiTriageResponses
(
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    TriageSessionId  INT NOT NULL,
    Response         NVARCHAR(MAX) NOT NULL,
    Analysis         NVARCHAR(500) NULL,
    CreatedAt        DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_AiTriageResponses_TriageSessions FOREIGN KEY (TriageSessionId) 
        REFERENCES TriageSessions(Id) ON DELETE CASCADE
);

CREATE TABLE ChatMessages
(
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    PatientId        INT NOT NULL,
    TriageSessionId  INT NULL,
    Role             NVARCHAR(20) NOT NULL,
    Content          NVARCHAR(MAX) NOT NULL,
    CreatedAt        DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_ChatMessages_Patients FOREIGN KEY (PatientId) 
        REFERENCES Patients(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ChatMessages_TriageSessions FOREIGN KEY (TriageSessionId) 
        REFERENCES TriageSessions(Id) ON DELETE SET NULL
);

-- ============================================================================
-- INDEXES FOR PERFORMANCE
-- ============================================================================

CREATE INDEX IX_Patients_Email ON Patients(Email);
CREATE INDEX IX_Patients_DomicileId ON Patients(DomicileId);
CREATE INDEX IX_Patients_PlaceOfBirthId ON Patients(PlaceOfBirthId);
CREATE INDEX IX_Patients_PreferredHospital ON Patients(PreferredHospitalId);
CREATE INDEX IX_Patients_FamilyMedicDoctor ON Patients(FamilyMedicDoctorId);

CREATE UNIQUE INDEX IX_DoctorProfiles_UserId ON DoctorProfiles(UserId);

CREATE INDEX IX_Hospitals_CreatedByDoctor ON Hospitals(CreatedByDoctorId);
CREATE INDEX IX_Hospitals_Location ON Hospitals(Country, County, City);

CREATE UNIQUE INDEX IX_DoctorHospitalMemberships_Doctor_Hospital_IsActive 
    ON DoctorHospitalMemberships(DoctorId, HospitalId, IsActive);

CREATE INDEX IX_MedicalDatas_PatientId ON MedicalDatas(PatientId);
CREATE INDEX IX_MedicalDatas_AuthorizedDoctor ON MedicalDatas(AuthorizedDoctorId);

CREATE INDEX IX_MedicalFiles_MedicalDataId ON MedicalFiles(MedicalDataId);

CREATE INDEX IX_PatientIssues_PatientId ON PatientIssues(PatientId);
CREATE INDEX IX_PatientIssues_IsActive ON PatientIssues(IsActive);

CREATE INDEX IX_FamilyMedicRequests_PatientId ON FamilyMedicRequests(PatientId);
CREATE INDEX IX_FamilyMedicRequests_DoctorEmail ON FamilyMedicRequests(DoctorEmail);
CREATE INDEX IX_FamilyMedicRequests_Status ON FamilyMedicRequests(Status);
CREATE INDEX IX_FamilyMedicRequests_DoctorId ON FamilyMedicRequests(DoctorId);

CREATE INDEX IX_TriageSessions_PatientId ON TriageSessions(PatientId);
CREATE INDEX IX_TriageSessions_IsActive ON TriageSessions(IsActive);

CREATE INDEX IX_TriageMessages_SessionId ON TriageMessages(SessionId);
CREATE INDEX IX_TriageMessages_CreatedAt ON TriageMessages(CreatedAt);

CREATE INDEX IX_AiTriageResponses_TriageSessionId ON AiTriageResponses(TriageSessionId);

CREATE INDEX IX_ChatMessages_PatientId ON ChatMessages(PatientId);
CREATE INDEX IX_ChatMessages_TriageSessionId ON ChatMessages(TriageSessionId);

-- ============================================================================
-- EF CORE MIGRATIONS HISTORY TABLE
-- ============================================================================

CREATE TABLE __EFMigrationsHistory 
(
    MigrationId    NVARCHAR(150) NOT NULL PRIMARY KEY,
    ProductVersion NVARCHAR(32) NOT NULL
);

-- Insert migration history records
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
('20251102161050_InitialCreate', '8.0.10'),
('20251110154456_AddHospitalsAndDoctorProfiles', '8.0.10'),
('20251110172353_UpdateMedicalDataModel', '8.0.10'),
('20251203000000_AddAiTriageTables', '8.0.10');

GO

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

PRINT '========================================';
PRINT 'DATABASE SETUP VERIFICATION';
PRINT '========================================';
PRINT '';

-- Count tables
DECLARE @TableCount INT;
SELECT @TableCount = COUNT(*) 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME != '__EFMigrationsHistory';

PRINT 'Total tables created: ' + CAST(@TableCount AS NVARCHAR(10));

-- List all tables
PRINT '';
PRINT 'Tables in database:';
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME != '__EFMigrationsHistory'
ORDER BY TABLE_NAME;

-- Count foreign keys
DECLARE @FKCount INT;
SELECT @FKCount = COUNT(*)
FROM sys.foreign_keys;

PRINT '';
PRINT 'Total foreign keys created: ' + CAST(@FKCount AS NVARCHAR(10));

-- Verify AI Triage tables
PRINT '';
PRINT 'AI Triage Tables Status:';
IF OBJECT_ID('dbo.TriageSessions', 'U') IS NOT NULL
    PRINT '✓ TriageSessions - OK'
ELSE
    PRINT '✗ TriageSessions - MISSING';

IF OBJECT_ID('dbo.TriageMessages', 'U') IS NOT NULL
    PRINT '✓ TriageMessages - OK'
ELSE
    PRINT '✗ TriageMessages - MISSING';

IF OBJECT_ID('dbo.AiTriageResponses', 'U') IS NOT NULL
    PRINT '✓ AiTriageResponses - OK'
ELSE
    PRINT '✗ AiTriageResponses - MISSING';

IF OBJECT_ID('dbo.ChatMessages', 'U') IS NOT NULL
    PRINT '✓ ChatMessages - OK'
ELSE
    PRINT '✗ ChatMessages - MISSING';

PRINT '';
PRINT '========================================';
PRINT 'Database setup complete!';
PRINT '========================================';
GO









USE MedicalTriageDB;
GO

-- Drop ChatMessages table if it exists (to recreate it correctly)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND type in (N'U'))
BEGIN
    DROP TABLE ChatMessages;
    PRINT 'Dropped existing ChatMessages table.';
END
GO

-- Create ChatMessages table with correct cascade behavior
CREATE TABLE ChatMessages
(
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    PatientId        INT NOT NULL,
    TriageSessionId  INT NULL,
    Role             NVARCHAR(20) NOT NULL,
    Content          NVARCHAR(MAX) NOT NULL,
    CreatedAt        DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    -- ✅ Changed to NO ACTION to avoid cascade path conflict
    CONSTRAINT FK_ChatMessages_Patients FOREIGN KEY (PatientId) 
        REFERENCES Patients(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_ChatMessages_TriageSessions FOREIGN KEY (TriageSessionId) 
        REFERENCES TriageSessions(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_ChatMessages_PatientId ON ChatMessages(PatientId);
CREATE INDEX IX_ChatMessages_TriageSessionId ON ChatMessages(TriageSessionId);

PRINT 'ChatMessages table created successfully!';
GO

-- ============================================================================
-- FINAL VERIFICATION
-- ============================================================================

PRINT '';
PRINT '========================================';
PRINT 'DATABASE VERIFICATION - ALL TABLES';
PRINT '========================================';
PRINT '';

-- Count all tables
DECLARE @TotalTables INT;
DECLARE @AITriageTables INT;

SELECT @TotalTables = COUNT(*) 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME != '__EFMigrationsHistory';

SELECT @AITriageTables = COUNT(*) 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' 
  AND (TABLE_NAME LIKE '%Triage%' OR TABLE_NAME = 'ChatMessages');

PRINT 'Total tables: ' + CAST(@TotalTables AS NVARCHAR(10));
PRINT 'AI Triage tables: ' + CAST(@AITriageTables AS NVARCHAR(10));
PRINT '';

-- List all tables
PRINT 'All tables in database:';
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME != '__EFMigrationsHistory'
ORDER BY TABLE_NAME;

PRINT '';
PRINT 'AI Triage Tables Status:';

IF OBJECT_ID('dbo.TriageSessions', 'U') IS NOT NULL
    PRINT '✓ TriageSessions - OK'
ELSE
    PRINT '✗ TriageSessions - MISSING';

IF OBJECT_ID('dbo.TriageMessages', 'U') IS NOT NULL
    PRINT '✓ TriageMessages - OK'
ELSE
    PRINT '✗ TriageMessages - MISSING';

IF OBJECT_ID('dbo.AiTriageResponses', 'U') IS NOT NULL
    PRINT '✓ AiTriageResponses - OK'
ELSE
    PRINT '✗ AiTriageResponses - MISSING';

IF OBJECT_ID('dbo.ChatMessages', 'U') IS NOT NULL
    PRINT '✓ ChatMessages - OK'
ELSE
    PRINT '✗ ChatMessages - MISSING';

-- Count foreign keys
DECLARE @FKCount INT;
SELECT @FKCount = COUNT(*) FROM sys.foreign_keys;
PRINT '';
PRINT 'Total foreign keys: ' + CAST(@FKCount AS NVARCHAR(10));

PRINT '';
PRINT '========================================';
PRINT 'DATABASE SETUP COMPLETE!';
PRINT '========================================';
PRINT 'You can now run your Blazor application.';
PRINT '';

GO