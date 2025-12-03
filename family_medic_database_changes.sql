-- =============================================
-- Family Medic Feature - Database Changes
-- =============================================
-- Run this script manually on your MedicalTriageDB database
-- This script adds support for family medic assignment
-- =============================================

USE MedicalTriageDB;
GO

-- =============================================
-- 1. Add Family Medic columns to Patients table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Patients') AND name = 'FamilyMedicEmail')
BEGIN
    ALTER TABLE dbo.Patients
    ADD FamilyMedicEmail NVARCHAR(200) NULL;
    
    PRINT 'Added FamilyMedicEmail column to Patients table';
END
ELSE
BEGIN
    PRINT 'FamilyMedicEmail column already exists in Patients table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Patients') AND name = 'FamilyMedicDoctorId')
BEGIN
    ALTER TABLE dbo.Patients
    ADD FamilyMedicDoctorId INT NULL;
    
    PRINT 'Added FamilyMedicDoctorId column to Patients table';
END
ELSE
BEGIN
    PRINT 'FamilyMedicDoctorId column already exists in Patients table';
END
GO

-- =============================================
-- 2. Create FamilyMedicRequests table
-- =============================================
IF OBJECT_ID('dbo.FamilyMedicRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FamilyMedicRequests
    (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        PatientId       INT NOT NULL,
        DoctorEmail     NVARCHAR(200) NOT NULL,
        Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending, Accepted, Rejected
        RequestedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        RespondedAt     DATETIME2 NULL,
        DoctorId        INT NULL, -- Set when doctor accepts
        OutlookToken    NVARCHAR(MAX) NULL, -- Encrypted refresh token for patient's Outlook account
        TokenExpiresAt  DATETIME2 NULL
    );
    
    PRINT 'Created FamilyMedicRequests table';
END
ELSE
BEGIN
    PRINT 'FamilyMedicRequests table already exists';
END
GO

-- =============================================
-- 3. Add Foreign Key Constraints
-- =============================================
-- Foreign key from Patients to DoctorProfiles for FamilyMedicDoctorId
-- Using NO ACTION to avoid multiple cascade paths (SQL Server limitation)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Patients_FamilyMedicDoctor')
BEGIN
    ALTER TABLE dbo.Patients
    ADD CONSTRAINT FK_Patients_FamilyMedicDoctor 
    FOREIGN KEY (FamilyMedicDoctorId) 
    REFERENCES dbo.DoctorProfiles(Id) 
    ON DELETE NO ACTION;
    
    PRINT 'Added FK_Patients_FamilyMedicDoctor foreign key';
END
ELSE
BEGIN
    PRINT 'FK_Patients_FamilyMedicDoctor foreign key already exists';
END
GO

-- Foreign key from FamilyMedicRequests to Patients
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FamilyMedicRequests_Patients')
BEGIN
    ALTER TABLE dbo.FamilyMedicRequests
    ADD CONSTRAINT FK_FamilyMedicRequests_Patients 
    FOREIGN KEY (PatientId) 
    REFERENCES dbo.Patients(Id) 
    ON DELETE CASCADE;
    
    PRINT 'Added FK_FamilyMedicRequests_Patients foreign key';
END
ELSE
BEGIN
    PRINT 'FK_FamilyMedicRequests_Patients foreign key already exists';
END
GO

-- Foreign key from FamilyMedicRequests to DoctorProfiles
-- Using NO ACTION to avoid multiple cascade paths (SQL Server limitation)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FamilyMedicRequests_DoctorProfiles')
BEGIN
    ALTER TABLE dbo.FamilyMedicRequests
    ADD CONSTRAINT FK_FamilyMedicRequests_DoctorProfiles 
    FOREIGN KEY (DoctorId) 
    REFERENCES dbo.DoctorProfiles(Id) 
    ON DELETE NO ACTION;
    
    PRINT 'Added FK_FamilyMedicRequests_DoctorProfiles foreign key';
END
ELSE
BEGIN
    PRINT 'FK_FamilyMedicRequests_DoctorProfiles foreign key already exists';
END
GO

-- =============================================
-- 4. Create Indexes for Performance
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Patients_FamilyMedicDoctorId')
BEGIN
    CREATE INDEX IX_Patients_FamilyMedicDoctorId 
    ON dbo.Patients(FamilyMedicDoctorId);
    
    PRINT 'Created index IX_Patients_FamilyMedicDoctorId';
END
ELSE
BEGIN
    PRINT 'Index IX_Patients_FamilyMedicDoctorId already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FamilyMedicRequests_PatientId')
BEGIN
    CREATE INDEX IX_FamilyMedicRequests_PatientId 
    ON dbo.FamilyMedicRequests(PatientId);
    
    PRINT 'Created index IX_FamilyMedicRequests_PatientId';
END
ELSE
BEGIN
    PRINT 'Index IX_FamilyMedicRequests_PatientId already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FamilyMedicRequests_DoctorEmail')
BEGIN
    CREATE INDEX IX_FamilyMedicRequests_DoctorEmail 
    ON dbo.FamilyMedicRequests(DoctorEmail);
    
    PRINT 'Created index IX_FamilyMedicRequests_DoctorEmail';
END
ELSE
BEGIN
    PRINT 'Index IX_FamilyMedicRequests_DoctorEmail already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FamilyMedicRequests_Status')
BEGIN
    CREATE INDEX IX_FamilyMedicRequests_Status 
    ON dbo.FamilyMedicRequests(Status);
    
    PRINT 'Created index IX_FamilyMedicRequests_Status';
END
ELSE
BEGIN
    PRINT 'Index IX_FamilyMedicRequests_Status already exists';
END
GO

-- =============================================
-- 5. Verify Changes
-- =============================================
PRINT '';
PRINT '=============================================';
PRINT 'Verification:';
PRINT '=============================================';

-- Check Patients table columns
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Patients' 
    AND COLUMN_NAME IN ('FamilyMedicEmail', 'FamilyMedicDoctorId')
ORDER BY COLUMN_NAME;

-- Check FamilyMedicRequests table
IF OBJECT_ID('dbo.FamilyMedicRequests', 'U') IS NOT NULL
BEGIN
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        IS_NULLABLE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'FamilyMedicRequests'
    ORDER BY ORDINAL_POSITION;
END

PRINT '';
PRINT 'Database changes completed successfully!';
PRINT '=============================================';
GO

