using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CRM.Microservice.Data;

/// <summary>Builds migration metadata without starting the service or contacting a database.</summary>
public sealed class CRMDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CRMDbContext>
{
    public CRMDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CRMDbContext>()
            .UseSqlServer("Server=localhost;Database=CRM_MigrationMetadata;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        return new CRMDbContext(options);
    }
}
