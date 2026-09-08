using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions;
using Identity.Microservice.Services.Implementation;
using Identity.Microservice.Services;
using Identity.Microservice.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowViteFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddControllers();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISystemRoleRepository, SystemRoleRepository>();
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();
builder.Services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
builder.Services.AddScoped<IUserAccessClaimRepository, UserAccessClaimRepository>();
builder.Services.AddScoped<IRepository<LeadCaptureRequest>, LeadCaptureRequestRepository>();
builder.Services.AddScoped<ILeadWorkflowRepository, LeadWorkflowRepository>();
builder.Services.AddScoped<IPricingPackageRepository, PricingPackageRepository>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Permission", policy =>
        policy.Requirements.Add(new PermissionRequirement(string.Empty)));
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddHttpContextAccessor();
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
    var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var userAccessClaimRepository = scope.ServiceProvider.GetRequiredService<IUserAccessClaimRepository>();
    var permissionRepository = scope.ServiceProvider.GetRequiredService<IPermissionRepository>();
    var systemRoleRepository = scope.ServiceProvider.GetRequiredService<ISystemRoleRepository>();
    var rolePermissionRepository = scope.ServiceProvider.GetRequiredService<IRolePermissionRepository>();
    var pricingPackageRepository = scope.ServiceProvider.GetRequiredService<IPricingPackageRepository>();

    IdentitySeedData.EnsureSeeded(db, config, tenantRepository, userRepository, userAccessClaimRepository, permissionRepository, systemRoleRepository, rolePermissionRepository, pricingPackageRepository);
}
app.UseCors("AllowViteFrontend");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "identity" }));
app.Run();
