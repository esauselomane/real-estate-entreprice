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
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowViteFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
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
builder.Services.AddScoped<IEmailService, EmailService>();

// 🎯 Email configuration binding
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// 📧 HTTP client for Mailpit API-based email delivery
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Mailpit", client =>
{
    var mailpitUrl = builder.Configuration.GetSection("Email:ApiUrl").Get<string>();
    if (!string.IsNullOrEmpty(mailpitUrl))
    {
        client.BaseAddress = new Uri(mailpitUrl);
    }
});

// 4. Configure Application Cryptographic JWT Verification Parameters
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretRealEstateEnterpriseCryptographicSigningKey123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Identity.Microservice";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RealEstateApp.Client";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    db.Database.EnsureCreated();
    IdentitySeedData.EnsureSeeded(db, config);
}

app.UseCors("AllowViteFrontend");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "identity" }));

app.Run();
