using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Identity.Microservice.Authorization;
using System.Security.Claims;
namespace Identity.Microservice.Controllers
{
    [ApiController]
    [Route("api/system/roles")]
    public class SystemRolesController : ControllerBase
    {
        private readonly IdentityDbContext _context;
        private readonly ILogger<SystemRolesController> _logger;
        public SystemRolesController(IdentityDbContext context, ILogger<SystemRolesController> logger)
        {
            _context = context;
            _logger = logger;
        }
        private bool IsPlatformAdmin()
        {
            var raw = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                        ?? User.FindFirst(ClaimTypes.Role)?.Value;
            var canonical = raw switch
            {
                "SuperAdmin" => "PlatformAdmin",
                "Manager" => "AgencyAdmin",
                "Admin" => "AgencyAdmin",
                "Agent" => "Broker",
                _ => raw
            };
            return canonical == "PlatformAdmin";
        }
        // GET api/system/roles  — list all role definitions with their permissions
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetSystemRoles()
        {
            var roles = await _context.SystemRoles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .OrderByDescending(r => r.IsPlatformScoped)
                .ThenBy(r => r.Name)
                .ToListAsync();
            _logger.LogInformation("Listed {Count} system role definitions", roles.Count);
            var result = roles.Select(r => new SystemRoleDefinitionResponse
            {
                RoleDefId = r.RoleDefId,
                Name = r.Name,
                Description = r.Description,
                IsPlatformScoped = r.IsPlatformScoped,
                IsAgencyScoped = r.IsAgencyScoped,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt,
                PermissionIds = r.RolePermissions.Where(rp => rp.Permission != null).Select(rp => rp.Permission!.PermissionId).ToList(),
                Permissions = r.RolePermissions.Where(rp => rp.Permission != null).Select(rp => new PermissionResponse
                {
                    PermissionId = rp.Permission!.PermissionId,
                    Key = rp.Permission.Key,
                    Module = rp.Permission.Module,
                    Action = rp.Permission.Action,
                    Description = rp.Permission.Description,
                    IsActive = rp.Permission.IsActive
                }).ToList()
            }).ToList();
            return Ok(result);
        }
        // GET api/system/roles/5
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSystemRole(int id)
        {
            var role = await _context.SystemRoles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.RoleDefId == id);
            if (role == null)
            {
                _logger.LogWarning("SystemRole lookup failed: RoleDefId={RoleDefId} not found", id);
                return NotFound(new { message = "Role definition not found." });
            }
            _logger.LogDebug("SystemRole retrieved: RoleDefId={RoleDefId} Name={Name}", role.RoleDefId, role.Name);
            var result = new SystemRoleDefinitionResponse
            {
                RoleDefId = role.RoleDefId,
                Name = role.Name,
                Description = role.Description,
                IsPlatformScoped = role.IsPlatformScoped,
                IsAgencyScoped = role.IsAgencyScoped,
                IsActive = role.IsActive,
                CreatedAt = role.CreatedAt,
                PermissionIds = role.RolePermissions.Where(rp => rp.Permission != null).Select(rp => rp.Permission!.PermissionId).ToList(),
                Permissions = role.RolePermissions.Where(rp => rp.Permission != null).Select(rp => new PermissionResponse
                {
                    PermissionId = rp.Permission!.PermissionId,
                    Key = rp.Permission.Key,
                    Module = rp.Permission.Module,
                    Action = rp.Permission.Action,
                    Description = rp.Permission.Description,
                    IsActive = rp.Permission.IsActive
                }).ToList()
            };
            return Ok(result);
        }
        // POST api/system/roles  — create a new role definition
        [HttpPost]
        [RequirePermission("system:roles:manage")]
        public async Task<IActionResult> CreateSystemRole([FromBody] RoleDefinitionRequest request)
        {
            if (!IsPlatformAdmin())
            {
                _logger.LogWarning("CreateSystemRole: forbidden — caller lacks PlatformAdmin role");
                return Forbid();
            }
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Role name is required." });
            var duplicate = await _context.SystemRoles.AnyAsync(r => r.Name == request.Name);
            if (duplicate)
                return BadRequest(new { message = "A role with that name already exists." });
            var role = new SystemRoleDefinition
            {
                Name = request.Name,
                Description = request.Description,
                IsPlatformScoped = request.IsPlatformScoped,
                IsAgencyScoped = request.IsAgencyScoped,
                PermissionsJson = "[]",
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };
            _context.SystemRoles.Add(role);
            await _context.SaveChangesAsync();
            // Assign permissions if provided
            if (request.PermissionIds != null && request.PermissionIds.Any())
            {
                foreach (var permissionId in request.PermissionIds.Distinct())
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleDefId = role.RoleDefId,
                        PermissionId = permissionId
                    });
                }
                await _context.SaveChangesAsync();
            }
            _logger.LogInformation("SystemRole created: RoleDefId={RoleDefId} Name={Name} PlatformScoped={PlatformScoped} AgencyScoped={AgencyScoped}",
                role.RoleDefId, role.Name, role.IsPlatformScoped, role.IsAgencyScoped);
            return Ok(new { message = "Role definition created.", roleId = role.RoleDefId });
        }
        // PUT api/system/roles/5  — update a role definition
        [HttpPut("{id:int}")]
        [RequirePermission("system:roles:manage")]
        public async Task<IActionResult> UpdateSystemRole(int id, [FromBody] RoleDefinitionRequest request)
        {
            if (!IsPlatformAdmin())
            {
                _logger.LogWarning("UpdateSystemRole: forbidden — caller lacks PlatformAdmin role");
                return Forbid();
            }
            var role = await _context.SystemRoles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleDefId == id);
            if (role == null)
            {
                _logger.LogWarning("UpdateSystemRole: RoleDefId={RoleDefId} not found", id);
                return NotFound(new { message = "Role definition not found." });
            }
            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name != role.Name)
            {
                var duplicate = await _context.SystemRoles.AnyAsync(r => r.Name == request.Name && r.RoleDefId != id);
                if (duplicate)
                    return BadRequest(new { message = "Another role with that name already exists." });
                role.Name = request.Name;
            }
            role.Description = request.Description;
            role.IsPlatformScoped = request.IsPlatformScoped;
            role.IsAgencyScoped = request.IsAgencyScoped;
            role.IsActive = request.IsActive;
            // Replace permissions if provided
            if (request.PermissionIds != null)
            {
                var existingPermissions = _context.RolePermissions.Where(rp => rp.RoleDefId == id);
                _context.RolePermissions.RemoveRange(existingPermissions);
                foreach (var permissionId in request.PermissionIds.Distinct())
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleDefId = id,
                        PermissionId = permissionId
                    });
                }
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation("SystemRole updated: RoleDefId={RoleDefId} Name={Name}", id, role.Name);
            return Ok(new { message = "Role definition updated.", roleId = role.RoleDefId });
        }
        // DELETE api/system/roles/5  — deactivate a role definition
        [HttpDelete("{id:int}")]
        [RequirePermission("system:roles:manage")]
        public async Task<IActionResult> DeleteSystemRole(int id)
        {
            if (!IsPlatformAdmin())
            {
                _logger.LogWarning("DeleteSystemRole: forbidden — caller lacks PlatformAdmin role");
                return Forbid();
            }
            var role = await _context.SystemRoles.FindAsync(id);
            if (role == null)
            {
                _logger.LogWarning("DeleteSystemRole: RoleDefId={RoleDefId} not found", id);
                return NotFound(new { message = "Role definition not found." });
            }
            // Reserved roles that drive the provisioning matrix cannot be deactivated.
            var reserved = new[] { "PlatformAdmin", "AgencyAdmin", "Broker", "Owner", "Tenant" };
            if (reserved.Contains(role.Name))
                return BadRequest(new { message = "Reserved role definitions cannot be deactivated." });
            role.IsActive = false;
            await _context.SaveChangesAsync();
            _logger.LogInformation("SystemRole deactivated: RoleDefId={RoleDefId} Name={Name}", id, role.Name);
            return Ok(new { message = "Role definition deactivated.", roleId = role.RoleDefId });
        }
    }
}
