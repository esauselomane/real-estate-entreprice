using CRM.Microservice.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Register REST Controller frameworks
builder.Services.AddControllers();

// 2. Configure Microsoft SQL Server Enterprise Database Engine Pool Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<CRMDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();
// 4. Ensure the database layout matches mappings and seed base data parameters cleanly during startup loops
using (var serviceScope = app.Services.CreateScope())
{
    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<CRMDbContext>();
    databaseContext.Database.EnsureDeleted();
    databaseContext.Database.EnsureCreated();
    CRMSeedData.EnsureSeeded(databaseContext);
}

// 5. Inject Pipeline Middleware routing and authorization components
app.UseRouting();
app.UseAuthorization();

// 6. Map public REST Controller endpoints
app.MapControllers();

// 7. Start the service listener engine application
app.Run();
