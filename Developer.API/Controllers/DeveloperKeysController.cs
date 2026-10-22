using Developer.API.DTOs;
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
        private readonly ILogger<DeveloperKeysController> _logger;

        public DeveloperKeysController(DeveloperDbContext db, ILogger<DeveloperKeysController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private bool IsPlatformAdministrator() => User.IsInRole("PlatformAdmin") || User.IsInRole("SuperAdmin");

        private bool CanManageAgency(string agencyId) => IsPlatformAdministrator() ||
            (!string.IsNullOrWhiteSpace(User.FindFirst("TenantId")?.Value) && User.FindFirst("TenantId")?.Value == agencyId);

        [HttpPost("generate")]
        [Authorize(Roles = "SuperAdmin,PlatformAdmin,AgencyAdmin,Manager,Admin")]
        public async Task<IActionResult> GenerateKey([FromBody] GenerateApiKeyRequest request)
        {
            var caller = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            _logger.LogInformation("GenerateKey: requested for AgencyId={AgencyId} By={Caller}", request.AgencyId, caller);

            if (string.IsNullOrWhiteSpace(request.AgencyId) || string.IsNullOrWhiteSpace(request.Name))
            {
                _logger.LogWarning("GenerateKey: rejected — missing AgencyId or Name");
                return BadRequest(new { message = "AgencyId and Name are required." });
            }

            if (!CanManageAgency(request.AgencyId)) return Forbid();
            if (request.ExpiresAt.HasValue && request.ExpiresAt <= DateTime.UtcNow)
                return BadRequest(new { message = "Expiry must be in the future." });
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
        [Authorize(Roles = "SuperAdmin,PlatformAdmin,AgencyAdmin,Manager,Admin")]
        public async Task<IActionResult> ListKeys([FromQuery] string? agencyId = null)
        {
            var caller = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            _logger.LogInformation("ListKeys: AgencyId filter={AgencyId} Caller={Caller}", agencyId, caller);

            if (!IsPlatformAdministrator()) agencyId = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrWhiteSpace(agencyId)) return BadRequest(new { message = "Select an agency." });
            if (!CanManageAgency(agencyId)) return Forbid();
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
        [Authorize(Roles = "SuperAdmin,PlatformAdmin,AgencyAdmin,Manager,Admin")]
        public async Task<IActionResult> RevokeKey(int id)
        {
            _logger.LogInformation("RevokeKey: KeyId={KeyId}", id);
            var key = await _db.AgencyApiKeys.FirstOrDefaultAsync(k => k.Id == id);
            if (key == null)
            {
                _logger.LogWarning("RevokeKey: KeyId={KeyId} not found", id);
                return NotFound(new { message = "API key not found." });
            }

            if (!CanManageAgency(key.AgencyId)) return Forbid();
            key.IsRevoked = true;
            await _db.SaveChangesAsync();

            _logger.LogInformation("API key revoked: KeyId={KeyId} AgencyId={AgencyId}", id, key.AgencyId);

            return Ok(new { message = "API key revoked successfully." });
        }
    }
}
