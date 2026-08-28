using Property.Microservice.Data;
using Property.Microservice.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTER CORE SYSTEM ROUTINGS
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 2. CONFIGURE MICROSOFT SQL SERVER ENTERPRISE DATABASE ENGINE POOL CONNECTION
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PropertyDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 3. 🛡️ REGISTER JWT BEARER AUTHENTICATION (mirrors Identity.Microservice signing keys)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "Identity.Microservice",
        ValidAudience = "RealEstateApp.Client",
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("SuperSecretRealEstateEnterpriseCryptographicSigningKey123!"))
    };
});
builder.Services.AddAuthorization();

// 4. INJECT EVENT-DRIVEN KAFKA STREAMING SERVICE AS A SINGLETON
builder.Services.AddSingleton<KafkaPublisherService>();

var app = builder.Build();
// 5. AUTOMATED DATABASE LIFECYCLE RECOVERY & DATA SEEDING LOOPS
using (var serviceScope = app.Services.CreateScope())
{
    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<PropertyDbContext>();
    databaseContext.Database.EnsureCreated();
    PropertySeedData.EnsureSeeded(databaseContext);
}

// 6. INJECT REQUEST PIPELINE MIDDLEWARE HANDLERS
//    Authentication MUST precede authorization so the principal is populated first.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// 7. MAP TARGET REST API ENDPOINT CONTROLLERS
app.MapControllers();

// 8. START THE MICROSERVICE WORKSPACE ENGINE LISTENER
app.Run();
