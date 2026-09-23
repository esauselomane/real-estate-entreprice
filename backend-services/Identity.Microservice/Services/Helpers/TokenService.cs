using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Microservice.Services
{
    public class TokenService(IConfiguration? configuration = null)
    {
        public string GenerateAccessToken(UserAccount account, UserAccessClaim permissions, TimeSpan? lifetime = null)
        {
            // Establish security signature parameters
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration?["Jwt:Secret"] ?? "SuperSecretRealEstateEnterpriseCryptographicSigningKey123!"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // 🔒 INJECT ENCRYPTED MULTI-TENANT ROLES AND CLAIMS ARRAYS
            var tokenClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.UserId),
                new Claim(ClaimTypes.Name, account.FullName),
                new Claim(ClaimTypes.Email, account.Email),
                new Claim(ClaimTypes.Role, account.SystemRole),
                
                // Central Multi-Tenant Isolation Discriminator Claim Vector
                new Claim("TenantId", account.TenantId),
                
                // Granular Runtime Permissions System Checks
                new Claim("CanDelete", permissions.CanDeleteProperties.ToString().ToLower()),
                new Claim("CanExportReports", permissions.CanExportFinancialReports.ToString().ToLower()),
                new Claim("CanBypassValuation", permissions.CanBypassValuationStages.ToString().ToLower()),

                // 🎯 Trial onboarding claims (carried to all downstream services for gating)
                new Claim("IsTrial", account.IsTrial.ToString().ToLower()),
                new Claim("TrialEndDate", account.TrialEndDate?.ToString("o") ?? "")
            };

            var tokenSignature = new JwtSecurityToken(
                issuer: configuration?["Jwt:Issuer"] ?? "Identity.Microservice",
                audience: configuration?["Jwt:Audience"] ?? "RealEstateApp.Client",
                claims: tokenClaims,
                expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(8)), // Valid for standard shift duration
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(tokenSignature);
        }
    }
}
