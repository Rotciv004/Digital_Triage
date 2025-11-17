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
        services.AddDbContext<MedicalTriageDbContext>(options =>
        {
            var provider = configuration.GetValue<string>("DatabaseProvider")?.ToLowerInvariant() ?? "mysql";
            var connectionString = configuration.GetConnectionString("MedicalTriageDb")
                ?? throw new InvalidOperationException("Connection string 'MedicalTriageDb' not found.");

            switch (provider)
            {
                case "sqlserver":
                case "mssql":
                    options.UseSqlServer(connectionString);
                    break;
                case "mysql":
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
            }
        });

        services.AddScoped<IHospitalService, HospitalService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IMedicalDataService, MedicalDataService>();
        services.AddScoped<IMedicalDataAuthorizationService, MedicalDataAuthorizationService>();
        services.AddScoped<IPatientIssueService, PatientIssueService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IFamilyMedicService, FamilyMedicService>();
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}

