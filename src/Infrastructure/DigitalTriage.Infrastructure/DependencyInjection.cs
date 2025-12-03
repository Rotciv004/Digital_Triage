using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Infrastructure.Persistence;
using DigitalTriage.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalTriage.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register DbContext Factory (for better concurrency)
        services.AddDbContextFactory<MedicalTriageDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("MedicalTriageDb"),
                b => b.MigrationsAssembly(typeof(MedicalTriageDbContext).Assembly.FullName)));
        
        // Also register regular DbContext for migrations
        services.AddDbContext<MedicalTriageDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("MedicalTriageDb"),
                b => b.MigrationsAssembly(typeof(MedicalTriageDbContext).Assembly.FullName)));

        // Register services
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IMedicalDataService, MedicalDataService>();
        services.AddScoped<IPatientIssueService, PatientIssueService>();
        services.AddScoped<IHospitalService, HospitalService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IFamilyMedicService, FamilyMedicService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAiTriageService, AiTriageService>();

        return services;
    }
}



