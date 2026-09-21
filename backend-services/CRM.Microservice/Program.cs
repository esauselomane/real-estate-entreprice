using CRM.Microservice.Data;
using CRM.Microservice.Models;
using CRM.Microservice.Services;
using CRM.Microservice.Services.Abstractions;
using CRM.Microservice.Services.BackgroundJobs;
using CRM.Microservice.Services.Implementation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<CRMDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretRealEstateEnterpriseCryptographicSigningKey123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Identity.Microservice";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RealEstateApp.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
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

builder.Services.AddAuthorization();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CRM Microservice", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Register repositories
builder.Services.AddScoped<IAgencyRepository, AgencyRepository>();
builder.Services.AddScoped<IBrokerRepository, BrokerRepository>();
builder.Services.AddScoped<ILeadRepository, LeadRepository>();
builder.Services.AddScoped<IDealRepository, DealRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<ILegalEntityRepository, LegalEntityRepository>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<IGeographyRepository, GeographyRepository>();
builder.Services.AddScoped<IOperationalRecordRepository, OperationalRecordRepository>();
builder.Services.AddScoped<IBrochureCardRepository, BrochureCardRepository>();
builder.Services.AddScoped<IRepository<BrochureCardLine>, BrochureCardLineRepository>();
builder.Services.AddScoped<IDealBrokerSplitRepository, DealBrokerSplitRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IRepository<BrokerMonthlyTarget>, BrokerMonthlyTargetRepository>();
builder.Services.AddScoped<IBrokerPerformanceMetricsRepository, BrokerPerformanceMetricsRepository>();
builder.Services.AddScoped<IBrokerScoreService, BrokerScoreService>();
builder.Services.AddScoped<IContactPersonRepository, ContactPersonRepository>();
builder.Services.AddScoped<IUserGrowthRepository, UserGrowthRepository>();

// Register background job queues
builder.Services.AddSingleton<IJobQueue<BrochureGenerationJob>, JobQueue<BrochureGenerationJob>>();
builder.Services.AddSingleton<IJobQueue<DataImportJob>, JobQueue<DataImportJob>>();

// Register background services
builder.Services.AddHostedService<BrochureGenerationBackgroundService>();
builder.Services.AddHostedService<DataImportBackgroundService>();

// Register services
builder.Services.AddScoped<PermissionCheckService>();
builder.Services.AddScoped<IPropertyCountService, PropertyCountService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CRMDbContext>();
    var agencyRepository = scope.ServiceProvider.GetRequiredService<IAgencyRepository>();
    var brokerRepository = scope.ServiceProvider.GetRequiredService<IBrokerRepository>();
    var leadRepository = scope.ServiceProvider.GetRequiredService<ILeadRepository>();
    var dealRepository = scope.ServiceProvider.GetRequiredService<IDealRepository>();
    var companyRepository = scope.ServiceProvider.GetRequiredService<ICompanyRepository>();
    var legalEntityRepository = scope.ServiceProvider.GetRequiredService<ILegalEntityRepository>();
    var contactRepository = scope.ServiceProvider.GetRequiredService<IContactRepository>();
    var geographyRepository = scope.ServiceProvider.GetRequiredService<IGeographyRepository>();
    var brochureCardRepository = scope.ServiceProvider.GetRequiredService<IBrochureCardRepository>();
    var dealBrokerSplitRepository = scope.ServiceProvider.GetRequiredService<IDealBrokerSplitRepository>();
    var notificationRepository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

    db.Database.Migrate();
    await CRMSeedData.EnsureSeededAsync(
        db,
        agencyRepository,
        brokerRepository,
        leadRepository,
        dealRepository,
        companyRepository,
        legalEntityRepository,
        contactRepository,
        geographyRepository,
        brochureCardRepository,
        dealBrokerSplitRepository,
        notificationRepository);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Shared.Access.Subscriptions.SubscriptionAccessMiddleware>();

app.MapControllers();

app.Run();