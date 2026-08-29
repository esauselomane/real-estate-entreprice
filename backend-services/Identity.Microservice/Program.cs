using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Identity.Microservice.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// 🛠️ MANDATORY EXPLICIT INJECTION NAMESPACES TO RESOLVE CS0411 INFERENCE

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTER THE EXPLICIT CORS POLICY PIPELINE
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowViteFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Your React client port
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Essential for auth cookie/token transfers
    });
});

// 1. Register REST Controller Routings
builder.Services.AddControllers();

// 2. 🛡️ Clean Framework Registration: Explicitly typing arguments to prevent cache desyncs
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 3. Inject our Secure Token Lifecycle Services
builder.Services.AddScoped<TokenService>();

// 4. Configure Application Cryptographic JWT Verification Parameters
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuperSecretRealEstateEnterpriseCryptographicSigningKey123!"))
        };
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    db.Database.EnsureCreated();
    IdentitySeedData.EnsureSeeded(db);
}

app.UseCors("AllowViteFrontend");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
