using Developer.API.Data;
using Developer.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Developer.API.Controllers
{
    [ApiController]
    [Route("api/developer/keys")]
    public class DeveloperKeysController : ControllerBase
    {
        private readonly DeveloperDbContext _db;

        public DeveloperKeysController(DeveloperDbContext db)
        {
            _db = db;
        }

        [HttpPost("generate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GenerateKey([FromBody] GenerateApiKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AgencyId) || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "AgencyId and Name are required." });

            var rawKey = ApiKeyGenerator.GenerateKey();
            var keyHash = ApiKeyGenerator.HashKey(rawKey);
            var keyPrefix = ApiKeyGenerator.GetPrefix(rawKey);

            var apiKey = new AgencyApiKey
            {
                AgencyId = request.AgencyId,
                AgencyName = request.AgencyName ?? request.AgencyId,
                KeyHash = keyHash,
                KeyPrefix = keyPrefix,
                Name = request.Name,
                Description = request.Description,
                AllowedIps = request.AllowedIps,
                ExpiresAt = request.ExpiresAt,
                CreatedBy = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.Identity?.Name
            };

            _db.AgencyApiKeys.Add(apiKey);
            await _db.SaveChangesAsync();

            var response = new GenerateApiKeyResponse
            {
                Id = apiKey.Id,
                AgencyId = apiKey.AgencyId,
                AgencyName = apiKey.AgencyName,
                Name = apiKey.Name,
                Description = apiKey.Description,
                AllowedIps = apiKey.AllowedIps,
                ExpiresAt = apiKey.ExpiresAt,
                ApiKey = rawKey,
                KeyPrefix = apiKey.KeyPrefix,
                CreatedBy = apiKey.CreatedBy,
                CreatedAt = apiKey.CreatedAt
            };

            return Ok(response);
        }

        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ListKeys([FromQuery] string? agencyId = null)
        {
            var query = _db.AgencyApiKeys.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(agencyId))
                query = query.Where(k => k.AgencyId == agencyId);

            var keys = await query
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new ApiKeyInfoResponse
                {
                    Id = k.Id,
                    AgencyId = k.AgencyId,
                    AgencyName = k.AgencyName,
                    Name = k.Name,
                    Description = k.Description,
                    KeyPrefix = k.KeyPrefix,
                    AllowedIps = k.AllowedIps,
                    ExpiresAt = k.ExpiresAt,
                    LastUsedAt = k.LastUsedAt,
                    IsRevoked = k.IsRevoked,
                    CreatedBy = k.CreatedBy,
                    CreatedAt = k.CreatedAt
                })
                .ToListAsync();

            return Ok(keys);
        }

        [HttpPost("{id:int}/revoke")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> RevokeKey(int id)
        {
            var key = await _db.AgencyApiKeys.FirstOrDefaultAsync(k => k.Id == id);
            if (key == null) return NotFound(new { message = "API key not found." });

            key.IsRevoked = true;
            await _db.SaveChangesAsync();

            return Ok(new { message = "API key revoked successfully." });
        }
    }
}
