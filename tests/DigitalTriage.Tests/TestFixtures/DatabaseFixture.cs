using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalTriage.Tests.TestFixtures;

/// <summary>
/// Fixture for creating in-memory database contexts for testing.
/// </summary>
public class DatabaseFixture    
{
    /// <summary>
    /// Creates a new instance of MedicalTriageDbContext with an in-memory database.
    /// </summary>
    /// <returns>A new DbContext instance for testing.</returns>
    public MedicalTriageDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MedicalTriageDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

 var context = new MedicalTriageDbContext(options);
 context.Database.EnsureCreated();
        return context;
    }
}
