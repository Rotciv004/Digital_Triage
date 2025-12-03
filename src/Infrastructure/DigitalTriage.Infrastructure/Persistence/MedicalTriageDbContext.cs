using DigitalTriage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTriage.Infrastructure.Persistence;

/// <summary>
/// Provides the EF Core database context for the MedicalTriage application, including Patients, Places, MedicalData, and PatientIssues.
/// </summary>
public class MedicalTriageDbContext(DbContextOptions<MedicalTriageDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<MedicalData> MedicalDatas => Set<MedicalData>();
    public DbSet<PlaceOfBirth> PlacesOfBirth => Set<PlaceOfBirth>();
    public DbSet<Domicile> Domiciles => Set<Domicile>();
    public DbSet<PatientIssue> PatientIssues => Set<PatientIssue>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();
    public DbSet<DoctorHospitalMembership> DoctorHospitalMemberships => Set<DoctorHospitalMembership>();
    public DbSet<MedicalFile> MedicalFiles => Set<MedicalFile>();
    public DbSet<FamilyMedicRequest> FamilyMedicRequests => Set<FamilyMedicRequest>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<TriageSession> TriageSessions => Set<TriageSession>();
    public DbSet<TriageMessage> TriageMessages => Set<TriageMessage>();
    public DbSet<AiTriageResponse> AiTriageResponses => Set<AiTriageResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Patient>()
            .HasOne(p => p.PlaceOfBirth)
            .WithMany()
            .HasForeignKey(p => p.PlaceOfBirthId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Patient>()
            .HasOne(p => p.Domicile)
            .WithMany()
            .HasForeignKey(p => p.DomicileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Patient>()
            .HasOne(p => p.PreferredHospital)
            .WithMany(h => h.AssignedPatients)
            .HasForeignKey(p => p.PreferredHospitalId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MedicalData>()
            .HasOne(md => md.Patient)
            .WithMany(p => p.MedicalDatas)
            .HasForeignKey(md => md.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MedicalData>()
            .HasOne(md => md.AuthorizedDoctor)
            .WithMany(d => d.AuthorizedMedicalData)
            .HasForeignKey(md => md.AuthorizedDoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MedicalData>()
            .Property(md => md.IsConfidential)
            .HasDefaultValue(true);

        modelBuilder.Entity<MedicalData>()
            .Property(md => md.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        modelBuilder.Entity<MedicalFile>()
            .HasOne(f => f.MedicalData)
            .WithMany(md => md.Files)
            .HasForeignKey(f => f.MedicalDataId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MedicalFile>()
            .Property(f => f.UploadDate)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        modelBuilder.Entity<PatientIssue>()
            .HasOne(i => i.Patient)
            .WithMany(p => p.Issues)
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DoctorProfile>()
            .HasOne(dp => dp.User)
            .WithOne(p => p.DoctorProfile)
            .HasForeignKey<DoctorProfile>(dp => dp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Hospital>()
            .HasOne(h => h.CreatedByDoctor)
            .WithMany(d => d.CreatedHospitals)
            .HasForeignKey(h => h.CreatedByDoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<DoctorHospitalMembership>()
            .HasOne(m => m.Doctor)
            .WithMany(d => d.HospitalMemberships)
            .HasForeignKey(m => m.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DoctorHospitalMembership>()
            .HasOne(m => m.Hospital)
            .WithMany(h => h.DoctorMemberships)
            .HasForeignKey(m => m.HospitalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DoctorHospitalMembership>()
            .HasIndex(m => new { m.DoctorId, m.HospitalId, m.IsActive })
            .HasDatabaseName("IX_DoctorHospitalMembership_Doctor_Hospital_IsActive");

        modelBuilder.Entity<Patient>()
            .HasOne(p => p.FamilyMedicDoctor)
            .WithMany(d => d.FamilyPatients)
            .HasForeignKey(p => p.FamilyMedicDoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FamilyMedicRequest>()
            .HasOne(fmr => fmr.Patient)
            .WithMany(p => p.FamilyMedicRequests)
            .HasForeignKey(fmr => fmr.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FamilyMedicRequest>()
            .HasOne(fmr => fmr.Doctor)
            .WithMany(d => d.FamilyMedicRequests)
            .HasForeignKey(fmr => fmr.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FamilyMedicRequest>()
            .Property(fmr => fmr.Status)
            .HasDefaultValue("Pending");

        modelBuilder.Entity<FamilyMedicRequest>()
            .Property(fmr => fmr.RequestedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // ChatMessage configuration (separate from TriageSession)
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            
            entity.HasOne(e => e.Patient)
                .WithMany()
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // ChatMessage can optionally link to a TriageSession, but TriageSession doesn't track ChatMessages
            entity.HasOne(e => e.TriageSession)
                .WithMany()
                .HasForeignKey(e => e.TriageSessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // TriageSession configuration
        modelBuilder.Entity<TriageSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Severity).HasMaxLength(20);
            entity.Property(e => e.RecommendedAction).HasMaxLength(500);
            entity.Property(e => e.StartedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            
            entity.HasOne(e => e.Patient)
                .WithMany()
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TriageMessage configuration (for AI triage messages)
        modelBuilder.Entity<TriageMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            
            entity.HasOne(e => e.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AiTriageResponse configuration
        modelBuilder.Entity<AiTriageResponse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Response).IsRequired();
            entity.Property(e => e.Analysis).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            
            entity.HasOne(e => e.TriageSession)
                .WithMany(s => s.AiTriageResponses)
                .HasForeignKey(e => e.TriageSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
