using Property.Microservice.Data;
using Property.Microservice.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTER CORE SYSTEM ROUTINGS
builder.Services.AddControllers();

// 2. CONFIGURE MICROSOFT SQL SERVER CONNECTION POOL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PropertyDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 3. INJECT EVENT-DRIVEN KAFKA STREAMING SERVICE AS A SINGLETON
builder.Services.AddSingleton<KafkaPublisherService>();

var app = builder.Build();
// 4. AUTOMATED DATABASE LIFECYCLE RECOVERY & DATA SEEDING LOOPS
using (var serviceScope = app.Services.CreateScope())
{
    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<PropertyDbContext>();
    databaseContext.Database.EnsureDeleted();
    databaseContext.Database.EnsureCreated();
    PropertySeedData.EnsureSeeded(databaseContext);
}

// 5. INJECT REQUEST PIPELINE MIDDLEWARE HANDLERS
app.UseRouting();
app.UseAuthorization();

// 6. MAP TARGET REST API ENDPOINT CONTROLLERS
app.MapControllers();

// 7. START THE MICROSERVICE WORKSPACE ENGINE LISTENER
app.Run();
