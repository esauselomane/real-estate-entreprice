using Developer.API.Data;
using Developer.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var developerConnection = builder.Configuration.GetConnectionString("DeveloperConnection");
var propertyConnection = builder.Configuration.GetConnectionString("PropertyConnection");

builder.Services.AddDbContext<DeveloperDbContext>(options =>
{
    options.UseSqlServer(developerConnection);
});

builder.Services.AddDbContext<PropertyReadDbContext>(options =>
{
    options.UseSqlServer(propertyConnection);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Identity.Microservice",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "RealEstateApp.Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "SuperSecretRealEstateEnterpriseCryptographicSigningKey123!"))
        };
    });

var app = builder.Build();

app.UseCors("AllowAll");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DeveloperDbContext>();
    db.Database.EnsureCreated();

    var keysTableExists = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AgencyApiKeys'")
        .AsEnumerable().FirstOrDefault();

    if (keysTableExists == 0)
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE [AgencyApiKeys] (
                [Id] int NOT NULL IDENTITY(1,1),
                [AgencyId] nvarchar(100) NOT NULL,
                [AgencyName] nvarchar(200) NOT NULL,
                [KeyHash] nvarchar(450) NOT NULL,
                [KeyPrefix] nvarchar(100) NOT NULL,
                [Name] nvarchar(450) NOT NULL,
                [Description] nvarchar(max) NULL,
                [AllowedIps] nvarchar(max) NULL,
                [ExpiresAt] datetime2 NULL,
                [LastUsedAt] datetime2 NULL,
                [IsRevoked] bit NOT NULL DEFAULT 0,
                [CreatedBy] nvarchar(max) NULL,
                [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                CONSTRAINT [PK_AgencyApiKeys] PRIMARY KEY ([Id]),
                CONSTRAINT [AK_AgencyApiKeys_KeyHash] UNIQUE ([KeyHash])
            );
            CREATE INDEX [IX_AgencyApiKeys_AgencyId_IsRevoked] ON [AgencyApiKeys] ([AgencyId], [IsRevoked]);
        ");
    }
}

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "developer-api" }));

app.Run();
