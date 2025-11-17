# Digital Triage App

A modern medical triage platform built with ASP.NET Core Blazor Server that enables patients to manage their medical information, receive AI-powered triage recommendations, and allows healthcare professionals to monitor patient data through an admin dashboard.

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Technology Stack](#technology-stack)
- [Installation](#installation)
- [Database Configuration](#database-configuration)
- [Running the Application](#running-the-application)
- [How the Application Works](#how-the-application-works)
- [Project Structure](#project-structure)
- [Configuration](#configuration)
- [User Roles](#user-roles)
- [Troubleshooting](#troubleshooting)

## Features

### For Patients
- **User Registration & Authentication**: Secure account creation with email and password (password hashing using BCrypt)
- **Hospital Preference Management**: Search existing hospitals, filter by location, preview doctors assigned to each facility, or accept the automatically suggested closest hospital based on domicile
- **Personal Information Management**: Store and update personal details including CNP, citizenship, place of birth, and domicile address
- **Doctor Profile Enrichment**: Doctors signing in as patients can maintain their specialization directly from the Personal Info page
- **Account Deletion**: Permanently delete account and all associated data with confirmation dialog (available in Personal Information page)
- **Medical Information Management**: Record and manage comprehensive medical data, assign an authorized doctor, control confidentiality, capture triage metrics (ESI level, estimated wait time), and attach supporting medical files
- **File Attachments**: Upload lab results or imaging to medical records with secure downloads and removal, stored under `wwwroot/uploads`
- **AI Triage Assistant**: Get preliminary medical triage recommendations based on symptoms (Preview mode - UI only)
- **Patient Issues Tracking**: Submit and track medical issues/concerns
- **Family Medic Assignment**: Assign a family doctor by email address and send medical problems to them via Outlook

### For Doctors/Administrators
- **Dedicated Doctor Registration**: Create doctor accounts with specialization details via `/register/doctor`
- **Hospital Management Workspace**: A dedicated `/hospital-management` page to create or update hospitals, search facilities, and manually join/leave memberships (creators are not joined automatically)
- **Admin Dashboard**: Access to comprehensive patient data, medical histories, attachments, and triage insights filtered by the hospitals the doctor currently belongs to
- **Family Patients Management**: Dedicated page (`/family-patients`) to view all family patients, accept/reject requests, and view patient medical information
- **Statistics Dashboard**: Advanced analytics page (`/statistics`) providing aggregated statistics on patients with active medical issues, including:
  - Filterable by location (country, county, city) and hospital
  - Time range filtering (last 7 days, 30 days, or custom range)
  - Distribution by severity (Low, Moderate, High, Critical) and problem type
  - Type × Severity heatmap matrix
  - Top hospitals ranking
  - Export functionality (CSV/JSON)
  - All data respects doctor permissions (only shows hospitals the doctor is a member of)
- **Patient Management**: View all registered patients and their information
- **Medical Records Access**: Review confidential flags, authorized-doctor relationships, supporting files, and detailed anamnesis for every patient
- **Streamlined Navigation**: Role-based menu visibility - Profile section hidden, only Administration section visible (Admin Dashboard + Hospital Management + Statistics)

## Prerequisites

Before running this application, ensure you have the following installed:

- **.NET 8.0 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **SQL Server** (SQL Server Express, Developer, or full edition) ([Download SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads))
- **SQL Server Management Studio (SSMS)** (optional, for database management) ([Download SSMS](https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms))
- **Visual Studio 2022** or **Visual Studio Code** (recommended for development)

## Technology Stack

- **Framework**: ASP.NET Core 8.0 (Blazor Server)
- **Database**: SQL Server (default) or MySQL (configurable via `DatabaseProvider`)
- **Authentication**: Cookie-based authentication with role-based authorization
- **Password Hashing**: BCrypt.Net-Next 4.0.3
- **UI Components**: Bootstrap 5 (via CDN), Bootstrap Icons
- **Rendering Mode**: Interactive Server Components

## Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd Program
   ```

2. **Restore NuGet packages**
   ```bash
   dotnet restore
   ```

3. **Configure the database connection** (see [Database Configuration](#database-configuration) section below)

4. **Apply database migrations**
   ```bash
   dotnet ef database update
   ```
   
   If you need to create a new migration:
   ```bash
   dotnet ef migrations add <MigrationName>
   ```

## Database Configuration

The application supports **SQL Server** (default) and **MySQL**. Set the desired provider in `appsettings*.json` via the `DatabaseProvider` key (`"SqlServer"` or `"MySql"`). The following steps show the SQL Server setup; see the note below for MySQL configuration.

### Step 1: Create the Database

1. Open **SQL Server Management Studio (SSMS)** or use `sqlcmd`
2. Connect to your SQL Server instance
3. Create a new database named `MedicalTriageDB`:
   ```sql
   CREATE DATABASE MedicalTriageDB;
   ```

### Step 2: Configure Connection String

1. Open `appsettings.Development.json` in the project root
2. Update the connection string to match your SQL Server configuration:

   ```json
   {
     "DatabaseProvider": "SqlServer",
     "ConnectionStrings": {
       "MedicalTriageDb": "Data Source=<YOUR_SERVER_NAME>\\<INSTANCE_NAME>;Initial Catalog=MedicalTriageDB;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Command Timeout=0"
     }
   }
   ```

   **Connection String Parameters:**
   - `<YOUR_SERVER_NAME>`: Your SQL Server name (e.g., `VICTOR`)
   - `<INSTANCE_NAME>`: Your SQL Server instance name (e.g., `SQLEXPRESS01`)
   - If using Windows Authentication, keep `Integrated Security=True`
   - For SQL Server Authentication, use:
     ```
    Data Source=SERVER\\INSTANCE;Initial Catalog=MedicalTriageDB;User Id=username;Password=password;Encrypt=True;TrustServerCertificate=True;
     ```

> **Using MySQL instead?**  
> Set `"DatabaseProvider": "MySql"` and supply a Pomelo-compatible connection string, e.g.  
> `server=localhost;port=3306;database=MedicalTriageDb;user=triage;password=StrongPassword!;AllowPublicKeyRetrieval=True;`

### Step 3: Create Database Schema

You have two options to create the database schema:

#### Option A: Using Entity Framework Migrations (Recommended)

After configuring the connection string, apply the database migrations:

```bash
dotnet ef database update
```

This will create all necessary tables automatically.

#### Option B: Using SQL Scripts (Manual Setup)

> ⚠️ **Prefer Entity Framework migrations.** Only use the script below if you must create the schema manually. After running it, insert migration rows manually into `__EFMigrationsHistory` so EF doesn’t try to recreate the tables.

**Complete SQL Script (schema current as of the latest update):**

```sql
USE MedicalTriageDB;
GO

/* Drop existing tables if you are rebuilding */
IF OBJECT_ID('dbo.FamilyMedicRequests','U') IS NOT NULL DROP TABLE dbo.FamilyMedicRequests;
IF OBJECT_ID('dbo.MedicalFiles','U') IS NOT NULL DROP TABLE dbo.MedicalFiles;
IF OBJECT_ID('dbo.DoctorHospitalMemberships','U') IS NOT NULL DROP TABLE dbo.DoctorHospitalMemberships;
IF OBJECT_ID('dbo.MedicalDatas','U') IS NOT NULL DROP TABLE dbo.MedicalDatas;
IF OBJECT_ID('dbo.PatientIssues','U') IS NOT NULL DROP TABLE dbo.PatientIssues;
IF OBJECT_ID('dbo.Hospitals','U') IS NOT NULL DROP TABLE dbo.Hospitals;
IF OBJECT_ID('dbo.DoctorProfiles','U') IS NOT NULL DROP TABLE dbo.DoctorProfiles;
IF OBJECT_ID('dbo.Patients','U') IS NOT NULL DROP TABLE dbo.Patients;
IF OBJECT_ID('dbo.Domiciles','U') IS NOT NULL DROP TABLE dbo.Domiciles;
IF OBJECT_ID('dbo.PlacesOfBirth','U') IS NOT NULL DROP TABLE dbo.PlacesOfBirth;

/* Base tables */
CREATE TABLE dbo.PlacesOfBirth
(
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    Country  NVARCHAR(100) NULL,
    County   NVARCHAR(100) NULL,
    City     NVARCHAR(100) NULL
);

CREATE TABLE dbo.Domiciles
(
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    Country  NVARCHAR(100) NULL,
    County   NVARCHAR(100) NULL,
    City     NVARCHAR(100) NULL,
    Street   NVARCHAR(150) NULL,
    Number   NVARCHAR(20)  NULL
);

CREATE TABLE dbo.Patients
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
    FamilyMedicDoctorId  INT NULL
);

CREATE TABLE dbo.DoctorProfiles
(
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT NOT NULL,
    Specialization NVARCHAR(150) NOT NULL
);

CREATE TABLE dbo.Hospitals
(
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    Name               NVARCHAR(200) NOT NULL,
    Country            NVARCHAR(100) NULL,
    County             NVARCHAR(100) NULL,
    City               NVARCHAR(100) NULL,
    Street             NVARCHAR(150) NULL,
    Number             NVARCHAR(20)  NULL,
    CreatedByDoctorId  INT NULL
);

CREATE TABLE dbo.DoctorHospitalMemberships
(
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    DoctorId    INT NOT NULL,
    HospitalId  INT NOT NULL,
    JoinedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LeftAt      DATETIME2 NULL,
    IsActive    BIT NOT NULL DEFAULT(1)
);

CREATE TABLE dbo.MedicalDatas
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
    IsConfidential            BIT NOT NULL DEFAULT(1),
    AuthorizedDoctorId        INT NULL,
    CreatedAt                 DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt                 DATETIME2 NULL,
    PatientId                 INT NOT NULL
);

CREATE TABLE dbo.MedicalFiles
(
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    MedicalDataId  INT NOT NULL,
    FileName       NVARCHAR(255) NOT NULL,
    FilePath       NVARCHAR(255) NOT NULL,
    UploadDate     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.PatientIssues
(
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    PatientId       INT NOT NULL,
    Title           NVARCHAR(200) NULL,
    Description     NVARCHAR(2000) NULL,
    ProblemType     NVARCHAR(100) NULL,
    EmergencyGrade  INT NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    LastUpdateDate  DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
);

CREATE TABLE dbo.FamilyMedicRequests
(
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    PatientId       INT NOT NULL,
    DoctorEmail     NVARCHAR(200) NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending, Accepted, Rejected
    RequestedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    RespondedAt     DATETIME2 NULL,
    DoctorId        INT NULL, -- Set when doctor accepts the request
    OutlookToken    NVARCHAR(MAX) NULL, -- Encrypted refresh token for patient's Outlook account
    TokenExpiresAt  DATETIME2 NULL
);

/* Foreign keys */
ALTER TABLE dbo.Patients  ADD CONSTRAINT FK_Patients_PlacesOfBirth FOREIGN KEY (PlaceOfBirthId) REFERENCES dbo.PlacesOfBirth(Id) ON DELETE NO ACTION;
ALTER TABLE dbo.Patients  ADD CONSTRAINT FK_Patients_Domiciles     FOREIGN KEY (DomicileId) REFERENCES dbo.Domiciles(Id) ON DELETE NO ACTION;
ALTER TABLE dbo.Patients  ADD CONSTRAINT FK_Patients_Hospitals     FOREIGN KEY (PreferredHospitalId) REFERENCES dbo.Hospitals(Id) ON DELETE SET NULL;
ALTER TABLE dbo.Patients  ADD CONSTRAINT FK_Patients_FamilyMedicDoctor FOREIGN KEY (FamilyMedicDoctorId) REFERENCES dbo.DoctorProfiles(Id) ON DELETE NO ACTION;

ALTER TABLE dbo.DoctorProfiles ADD CONSTRAINT FK_DoctorProfiles_Patients FOREIGN KEY (UserId) REFERENCES dbo.Patients(Id) ON DELETE CASCADE;
ALTER TABLE dbo.Hospitals ADD CONSTRAINT FK_Hospitals_DoctorProfiles FOREIGN KEY (CreatedByDoctorId) REFERENCES dbo.DoctorProfiles(Id) ON DELETE SET NULL;
ALTER TABLE dbo.DoctorHospitalMemberships ADD CONSTRAINT FK_DoctorHospitalMemberships_Doctors   FOREIGN KEY (DoctorId)   REFERENCES dbo.DoctorProfiles(Id) ON DELETE CASCADE;
ALTER TABLE dbo.DoctorHospitalMemberships ADD CONSTRAINT FK_DoctorHospitalMemberships_Hospitals FOREIGN KEY (HospitalId) REFERENCES dbo.Hospitals(Id)       ON DELETE CASCADE;
ALTER TABLE dbo.MedicalDatas ADD CONSTRAINT FK_MedicalDatas_Patients          FOREIGN KEY (PatientId)          REFERENCES dbo.Patients(Id)        ON DELETE CASCADE;
ALTER TABLE dbo.MedicalDatas ADD CONSTRAINT FK_MedicalDatas_DoctorProfiles    FOREIGN KEY (AuthorizedDoctorId) REFERENCES dbo.DoctorProfiles(Id) ON DELETE SET NULL;
ALTER TABLE dbo.MedicalFiles ADD CONSTRAINT FK_MedicalFiles_MedicalDatas FOREIGN KEY (MedicalDataId) REFERENCES dbo.MedicalDatas(Id) ON DELETE CASCADE;
ALTER TABLE dbo.PatientIssues ADD CONSTRAINT FK_PatientIssues_Patients FOREIGN KEY (PatientId) REFERENCES dbo.Patients(Id) ON DELETE CASCADE;
ALTER TABLE dbo.FamilyMedicRequests ADD CONSTRAINT FK_FamilyMedicRequests_Patients FOREIGN KEY (PatientId) REFERENCES dbo.Patients(Id) ON DELETE CASCADE;
ALTER TABLE dbo.FamilyMedicRequests ADD CONSTRAINT FK_FamilyMedicRequests_DoctorProfiles FOREIGN KEY (DoctorId) REFERENCES dbo.DoctorProfiles(Id) ON DELETE NO ACTION;

/* Indexes */
CREATE INDEX IX_Patients_DomicileId        ON dbo.Patients(DomicileId);
CREATE INDEX IX_Patients_PlaceOfBirthId    ON dbo.Patients(PlaceOfBirthId);
CREATE INDEX IX_Patients_PreferredHospital ON dbo.Patients(PreferredHospitalId);
CREATE INDEX IX_Patients_FamilyMedicDoctor ON dbo.Patients(FamilyMedicDoctorId);
CREATE INDEX IX_DoctorProfiles_UserId      ON dbo.DoctorProfiles(UserId);
CREATE INDEX IX_Hospitals_CreatedByDoctor  ON dbo.Hospitals(CreatedByDoctorId);
CREATE UNIQUE INDEX IX_DoctorHospitalMemberships_Doctor_Hospital_IsActive ON dbo.DoctorHospitalMemberships(DoctorId, HospitalId, IsActive);
CREATE INDEX IX_MedicalDatas_PatientId        ON dbo.MedicalDatas(PatientId);
CREATE INDEX IX_MedicalDatas_AuthorizedDoctor ON dbo.MedicalDatas(AuthorizedDoctorId);
CREATE INDEX IX_MedicalFiles_MedicalDataId    ON dbo.MedicalFiles(MedicalDataId);
CREATE INDEX IX_PatientIssues_PatientId       ON dbo.PatientIssues(PatientId);
CREATE INDEX IX_FamilyMedicRequests_PatientId ON dbo.FamilyMedicRequests(PatientId);
CREATE INDEX IX_FamilyMedicRequests_DoctorEmail ON dbo.FamilyMedicRequests(DoctorEmail);
CREATE INDEX IX_FamilyMedicRequests_Status ON dbo.FamilyMedicRequests(Status);
GO

/* Optionally mark migrations as applied */
IF OBJECT_ID('__EFMigrationsHistory','U') IS NULL
BEGIN
    CREATE TABLE __EFMigrationsHistory (MigrationId NVARCHAR(150) NOT NULL PRIMARY KEY, ProductVersion NVARCHAR(32) NOT NULL);
END;
MERGE __EFMigrationsHistory AS target
USING (VALUES
    ('20251102161050_InitialCreate','9.0.10'),
    ('20251110154456_AddHospitalsAndDoctorProfiles','9.0.10'),
    ('20251110172353_UpdateMedicalDataModel','9.0.10')
) AS source(MigrationId, ProductVersion)
ON target.MigrationId = source.MigrationId
WHEN NOT MATCHED THEN INSERT (MigrationId, ProductVersion) VALUES (source.MigrationId, source.ProductVersion);
GO
```

**What this script creates:**
- Full hospital/doctor infrastructure (including membership table with cascade deletes)
- Extended `MedicalDatas` schema with anamnesis, triage (ESI) and privacy fields plus authorized doctor linkage
- `MedicalFiles` table for attachments (cascade delete per medical record)
- Enhanced `PatientIssues` table with statistics fields:
  - `ProblemType` (NVARCHAR(100)): Clinical category for filtering
  - `EmergencyGrade` (INT): ESI level for severity classification
  - `IsActive` (BIT): Status flag (active/resolved) - required for statistics visibility
  - `LastUpdateDate` (DATETIMEOFFSET): Timestamp for time-based filtering
- **Family Medic feature tables and columns:**
  - `FamilyMedicEmail` (NVARCHAR(200)): Email address of the assigned family medic (in `Patients` table)
  - `FamilyMedicDoctorId` (INT): Foreign key to `DoctorProfiles` table (in `Patients` table)
  - `FamilyMedicRequests` table: Tracks family medic assignment requests with status (Pending/Accepted/Rejected)
  - `OutlookToken` (NVARCHAR(MAX)): Encrypted refresh token for patient's Outlook account (in `FamilyMedicRequests` table)
  - `TokenExpiresAt` (DATETIME2): Expiration date for Outlook token
- Role-based patient schema used by the current application
- All necessary indexes and foreign key constraints

**PatientIssues Statistics Fields:**
- All new issues created through the application are automatically set to `IsActive = 1` and `LastUpdateDate = current time`
- Existing issues need to be activated using the UPDATE script provided above
- Issues with `IsActive = 0` will not appear in statistics
- Issues without a `LastUpdateDate` will not appear in time-filtered statistics

**Note for Existing Databases:** If you already have a database and need to add the new PatientIssues columns, run these ALTER TABLE commands:

```sql
USE MedicalTriageDB;
GO

-- Add ProblemType column (clinical category)
ALTER TABLE dbo.PatientIssues
ADD ProblemType NVARCHAR(100) NULL;

-- Add EmergencyGrade column (ESI level - stored as INT, nullable)
ALTER TABLE dbo.PatientIssues
ADD EmergencyGrade INT NULL;

-- Add IsActive column (default to 1 for existing records)
ALTER TABLE dbo.PatientIssues
ADD IsActive BIT NOT NULL DEFAULT 1;

-- Add LastUpdateDate column (default to current time for existing records)
ALTER TABLE dbo.PatientIssues
ADD LastUpdateDate DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET();
GO

-- Activate all existing patient issues for statistics
-- This ensures existing issues appear in the statistics dashboard
UPDATE dbo.PatientIssues
SET 
    IsActive = 1,
    LastUpdateDate = CASE 
        WHEN LastUpdateDate IS NULL OR LastUpdateDate < CreatedAt 
        THEN CreatedAt 
        ELSE LastUpdateDate 
    END
WHERE IsActive IS NULL OR IsActive = 0;
GO

-- Verify the update
SELECT 
    COUNT(*) AS TotalIssues,
    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveIssues,
    SUM(CASE WHEN ProblemType IS NOT NULL THEN 1 ELSE 0 END) AS IssuesWithProblemType,
    SUM(CASE WHEN EmergencyGrade IS NOT NULL THEN 1 ELSE 0 END) AS IssuesWithEmergencyGrade
FROM dbo.PatientIssues;
GO
```

**Important Notes:**
- **PatientIssues Fields:**
  - `ProblemType`: Clinical category (e.g., "Cardiac", "Respiratory", "Neurological") - optional but recommended for better filtering
  - `EmergencyGrade`: ESI (Emergency Severity Index) level stored as INT (1=Resuscitation, 2=Critical, 3=Urgent, 4=NonUrgent, 5=Consult) - optional
  - `IsActive`: Boolean flag indicating if the issue is still active (default: true) - **required for statistics**
  - `LastUpdateDate`: Timestamp for tracking issue updates - **required for time-based filtering**
- **Statistics Requirements:** For patient issues to appear in the statistics dashboard:
  1. The issue must have `IsActive = 1`
  2. The patient must have a `PreferredHospitalId` assigned
  3. The doctor viewing statistics must be a member of that hospital
  4. The issue's `LastUpdateDate` must fall within the selected time range (if a time filter is applied)

After running the script, EF Core believes all migrations are applied thanks to the final MERGE statement. You can disable that part if you prefer to manage migration history manually.

### Step 3.5: Apply Family Medic Database Changes (If Using Manual Setup)

If you're using manual SQL scripts instead of EF migrations, you also need to run the family medic database changes script:

```bash
# Run the family medic database changes script
sqlcmd -S <YOUR_SERVER_NAME>\<INSTANCE_NAME> -d MedicalTriageDB -i family_medic_database_changes.sql
```

Or execute `family_medic_database_changes.sql` manually in SSMS. This script:
- Adds `FamilyMedicEmail` and `FamilyMedicDoctorId` columns to the `Patients` table
- Creates the `FamilyMedicRequests` table for tracking family medic assignment requests
- Adds necessary foreign keys and indexes
- Handles existing databases safely (checks for existing columns/tables before creating)

**Note:** If you're using EF migrations, the family medic schema will be included automatically when you run `dotnet ef database update`.

### Step 4: Verify Database Setup

After creating the schema (using either method), verify the database was created successfully:

```sql
USE MedicalTriageDB;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;
```

You should see all the tables listed:
- `Domiciles`
- `DoctorHospitalMemberships`
- `DoctorProfiles`
- `FamilyMedicRequests` (new - for family medic feature)
- `Hospitals`
- `MedicalDatas`
- `MedicalFiles`
- `PatientIssues`
- `Patients` (includes `FamilyMedicEmail` and `FamilyMedicDoctorId` columns)
- `PlacesOfBirth`

You can also verify the foreign key relationships:

```sql
USE MedicalTriageDB;
SELECT 
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ParentTable,
    cp.name AS ParentColumn,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    cr.name AS ReferencedColumn
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns AS cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
INNER JOIN sys.columns AS cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id
ORDER BY ParentTable, ForeignKeyName;
```

## Running the Application

1. **Navigate to the presentation project**
   ```bash
   cd src/Presentation/DigitalTriage.Presentation
   ```

2. **Run the application**
   ```bash
   dotnet run
   ```

   Or use a specific profile:
   ```bash
   dotnet run --launch-profile https
   ```

3. **Open your browser** and navigate to:
   - HTTPS: `https://localhost:7266`
   - HTTP: `http://localhost:5180`

4. **First-time setup**: Create a new patient account at `/register`, or create a doctor account with specialization details at `/register/doctor`

## How the Application Works

### Authentication Flow

1. **Registration**: 
   - Patients sign up via `/register`; doctors sign up via `/register/doctor` and provide their medical specialization
   - Passwords are hashed using BCrypt before storage
   - A default `MedicalData` record is created for every new account
   - Patients automatically receive the "Patient" role; doctors created through the doctor registration flow receive the "Doctor" role
   - Patients without a preferred hospital are auto-assigned to the closest matching hospital based on domicile (if available)

2. **Login**:
   - Users authenticate with email and password
   - Cookie-based session is created (expires after 8 hours)
   - Doctor accounts use the same login endpoint after they have been created through the doctor registration flow

3. **Authorization**:
   - Role-based access control using ASP.NET Core Authorization
   - Patients can access their own data
   - Doctors can access the admin dashboard

### Application Structure

- **Razor Components**: Blazor Server components for interactive UI
- **Razor Pages**: Traditional Razor Pages for login (located in `Pages/Account/Login.cshtml`)
- **Services Layer**: Business logic separated into service classes:
  - `PatientService`: Patient CRUD operations and authentication
  - `HospitalService`: Hospital creation, membership management, and patient hospital assignment
  - `MedicalDataService`: Medical data management
  - `PatientIssueService`: Issue tracking and deletion
  - `StatisticsService`: Statistics and analytics for patient issues with filtering capabilities
  - `FamilyMedicService`: Family medic assignment, request management, and Outlook token storage
  - `EmailService`: Email sending via Microsoft Graph API (Outlook integration)
- **Data Layer**: Entity Framework Core with `MedicalTriageDbContext`
- **Helpers**: Utility classes for authentication and antiforgery protection
- **API Controllers**: RESTful endpoints for statistics (`/api/stats/*`) accessible to doctors only

### Key Features Implementation

1. **Personal Information Management**:
   - Patients can update personal details including CNP, citizenship, addresses
   - Place of birth and domicile are stored as separate entities
   - **Account Deletion**: Patients can permanently delete their account with a confirmation dialog
   - Deletion removes all associated data: personal info, medical data, reported issues, and related addresses (if not shared)

2. **Medical Information**:
   - One-to-many relationship: Patients can have multiple medical data records
   - Stores comprehensive medical history

3. **AI Triage**:
   - Currently in preview mode (UI placeholder)
   - Interface is ready for integration with AI/ML services

4. **Medical Attachments & Privacy**:
   - Patients can upload, download, and remove supporting files from their medical record
   - Attachments are scoped to the owning patient and secured behind antiforgery-protected API endpoints
   - Only the patient and the explicitly authorized doctor for a record can access confidential files

5. **Admin Dashboard**:
   - Available only to users with "Doctor" role
   - Provides a comprehensive view of patient medical records—including attachments and confidentiality status—filtered by hospitals the doctor currently belongs to

6. **Statistics Dashboard**:
   - Accessible at `/statistics` for doctors only
   - Displays aggregated statistics on patients with active medical issues
   - **Filtering Capabilities**:
     - Location-based filtering (country, county, or city level)
     - Hospital-specific filtering (optional)
     - Time range selection (last 7 days, 30 days, or custom date range)
     - Multi-select filters for problem types and emergency grades (ESI levels)
   - **Visualizations**:
     - KPI cards showing total active issues, unique patients, and hospitals included
     - Bar charts for severity distribution (Low, Moderate, High, Critical)
     - Bar charts for problem type distribution
     - Heatmap table showing Type × Severity matrix
     - Top hospitals ranking table (when no hospital filter is applied)
   - **Data Export**: Export filtered statistics in CSV or JSON format
   - **Security**: All statistics respect doctor permissions—only shows data for hospitals the doctor is an active member of
   - **Patient Issues Enhancement**: Patient issues now include:
     - `ProblemType`: Clinical category (e.g., "Cardiac", "Respiratory", "Neurological")
     - `EmergencyGrade`: ESI (Emergency Severity Index) level for triage prioritization
     - `IsActive`: Boolean flag indicating if the issue is still active
     - `LastUpdateDate`: Timestamp for tracking issue updates

7. **Hospital & Doctor Collaboration**:
   - Doctors manage hospitals on a dedicated `/hospital-management` page, manually joining/leaving facilities (creators are not auto-joined)
   - Hospitals with no active doctors are automatically deleted to prevent orphaned facilities
   - Patients can search existing hospitals, preview assigned doctors, and select their preferred facility

8. **User Interface Enhancements**:
   - **Fixed Sidebar Navigation**: Navigation menu remains visible while scrolling page content
   - **Role-Based Menu**: Profile section (Personal Information, Medical Information, AI Triage) only visible to patients
   - Doctors see a streamlined menu with Home, Admin Dashboard, Hospital Management, Statistics, Family Patients, and Exit options

9. **Family Medic Feature**:
   - **Patient Assignment**: Patients can assign a family medic by entering the doctor's email address in the Medical Information page
   - **Request System**: When a patient requests a doctor as their family medic, the doctor receives an in-app notification
   - **Accept/Reject**: Doctors can accept or reject family medic requests from the Family Patients page
   - **Family Patients Dashboard**: Doctors have a dedicated page (`/family-patients`) to view all their family patients, see their medical information, and manage pending requests
   - **Email Integration**: After AI triage (if not an emergency), patients can send their medical problem to their family medic via Outlook email
   - **Outlook OAuth2**: Patients can connect their Outlook account to enable email sending (see `OUTLOOK_OAUTH_SETUP.md` for setup instructions)
   - **Change Family Medic**: Patients can change their family medic at any time, which requires the new doctor to accept the request

## Project Structure

```
Program/
├── src/
│   ├── Domain/
│   │   └── DigitalTriage.Domain/            # Core entities and value objects
│   ├── Application/
│   │   └── DigitalTriage.Application/       # Service/repository contracts and shared abstractions
│   ├── Infrastructure/
│   │   └── DigitalTriage.Infrastructure/    # EF Core DbContext, data services, DI extensions
│   └── Presentation/
│       └── DigitalTriage.Presentation/      # Blazor UI, controllers, static assets, configuration
├── DigitalTriageApp.sln                     # Solution entry point
└── README.md                                # Documentation
```

## Configuration

### Application Settings

Key configuration in `appsettings.Development.json`:

- **ConnectionStrings**: Database connection string
- **Logging**: Log level configuration
- **BaseUrl**: API base URL (defaults to `https://localhost:7266`)
- **MicrosoftGraph**: Microsoft Graph API configuration for Outlook integration (optional):
  - `ClientId`: Azure AD application client ID
  - `ClientSecret`: Azure AD application client secret
  - `TenantId`: Azure AD tenant ID (use "common" for multi-tenant)
- **Encryption**: Encryption key for storing sensitive data (e.g., Outlook tokens):
  - `Key`: 32-byte encryption key (must be exactly 32 characters)

**Note:** Outlook integration is optional. If `MicrosoftGraph:ClientId` and `MicrosoftGraph:ClientSecret` are not configured, the application will run normally but Outlook features will be disabled. See `OUTLOOK_SETUP_INSTRUCTIONS.md` for detailed setup instructions.

### Cookie Authentication Settings

Configured in `Program.cs`:
- **LoginPath**: `/Account/Login`
- **LogoutPath**: `/logout`
- **ExpireTimeSpan**: 8 hours
- **HttpOnly**: Enabled (security)
- **SameSite**: Lax mode
- **SecurePolicy**: SameAsRequest (Development), Always (Production)

### Antiforgery Protection

- Blazor forms use the `<AntiforgeryToken />` component and the `AntiforgeryHelper` service to retrieve the token.
- JavaScript helpers in `wwwroot/js/antiforgery.js` expose `postWithAntiforgery` for manual fetch requests.
- The `MedicalFilesController` uploads, deletes and downloads require the antiforgery header (`X-CSRF-TOKEN`) and enforce the same protection used throughout the app.
- Tokens are issued per-request and stored in the `__RequestVerificationToken` cookie; ensure this cookie is present when calling API endpoints from custom clients.