using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Identity.Microservice.Authorization;
using System.Security.Claims;
namespace Identity.Microservice.Controllers
{
    [ApiController]
    [Route("api/system/permissions")]
    public class PermissionsController : ControllerBase
    {
        private readonly IdentityDbContext _context;
        private readonly ILogger<PermissionsController> _logger;
        public PermissionsController(IdentityDbContext context, ILogger<PermissionsController> logger)
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
        // GET api/permissions
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPermissions()
        {
            var permissions = await _context.Permissions
                .OrderBy(p => p.Key)
                .ToListAsync();
            _logger.LogInformation("Listed {Count} permissions", permissions.Count);
            var result = permissions.Select(p => new PermissionResponse
            {
                PermissionId = p.PermissionId,
                Key = p.Key,
                Module = p.Module,
                Action = p.Action,
                Description = p.Description,
                IsActive = p.IsActive
            }).ToList();
            return Ok(result);
        }
        // GET api/system/permissions/5
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPermission(int id)
        {
            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null)
            {
                _logger.LogWarning("Permission lookup failed: PermissionId={PermissionId} not found", id);
                return NotFound(new { message = "Permission not found." });
            }
            _logger.LogDebug("Permission retrieved: PermissionId={PermissionId} Key={Key}", permission.PermissionId, permission.Key);
            var result = new PermissionResponse
            {
                PermissionId = permission.PermissionId,
                Key = permission.Key,
                Module = permission.Module,
                Action = permission.Action,
                Description = permission.Description,
                IsActive = permission.IsActive
            };
            return Ok(result);
        }
        // POST api/system/permissions
        [HttpPost]
        [RequirePermission("system:permissions:manage")]
        public async Task<IActionResult> CreatePermission([FromBody] PermissionRequest request)
        {
            if (!IsPlatformAdmin())
            {
                _logger.LogWarning("CreatePermission: forbidden — caller lacks PlatformAdmin role");
                return Forbid();
            }
            if (string.IsNullOrWhiteSpace(request.Key))
                return BadRequest(new { message = "Permission key is required." });
            if (string.IsNullOrWhiteSpace(request.Description))
                return BadRequest(new { message = "Permission description is required." });
            var duplicate = await _context.Permissions.AnyAsync(p => p.Key == request.Key);
            if (duplicate)
                return BadRequest(new { message = "A permission with that key already exists." });
            var permission = new Permission
            {
                Key = request.Key,
                Module = !string.IsNullOrWhiteSpace(request.Module) ? request.Module : ParseModule(request.Key),
                Action = !string.IsNullOrWhiteSpace(request.Action) ? request.Action : ParseAction(request.Key),
                Description = request.Description,
                IsActive = request.IsActive
            };
            _context.Permissions.Add(permission);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Permission created: PermissionId={PermissionId} Key={Key}", permission.PermissionId, permission.Key);
            var result = new PermissionResponse
            {
                PermissionId = permission.PermissionId,
                Key = permission.Key,
                Module = permission.Module,
                Action = permission.Action,
                Description = permission.Description,
                IsActive = permission.IsActive
            };
            return Ok(result);
        }
        // PUT api/system/permissions/5
        [HttpPut("{id:int}")]
        [RequirePermission("system:permissions:manage")]
        public async Task<IActionResult> UpdatePermission(int id, [FromBody] PermissionRequest request)
        {
            if (!IsPlatformAdmin())
            {
                _logger.LogWarning("UpdatePermission: forbidden — caller lacks PlatformAdmin role");
                return Forbid();
            }
            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null)
            {
                _logger.LogWarning("UpdatePermission: PermissionId={PermissionId} not found", id);
                return NotFound(new { message = "Permission not found." });
            }
            if (!string.IsNullOrWhiteSpace(request.Key) && request.Key != permission.Key)
            {
                var duplicate = await _context.Permissions.AnyAsync(p => p.Key == request.Key && p.PermissionId != id);
                if (duplicate)
                    return BadRequest(new { message = "Another permission with that key already exists." });
                permission.Key = request.Key;
            }
            if (!string.IsNullOrWhiteSpace(request.Description))
                permission.Description = request.Description;
            if (!string.IsNullOrWhiteSpace(request.Module))
                permission.Module = request.Module;
            else if (!string.IsNullOrWhiteSpace(request.Key))
                permission.Module = ParseModule(request.Key);
            if (!string.IsNullOrWhiteSpace(request.Action))
                permission.Action = request.Action;
            else if (!string.IsNullOrWhiteSpace(request.Key))
                permission.Action = ParseAction(request.Key);
            permission.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Permission updated: PermissionId={PermissionId} Key={Key}", id, permission.Key);
            var result = new PermissionResponse
            {
                PermissionId = permission.PermissionId,
                Key = permission.Key,
                Module = permission.Module,
                Action = permission.Action,
                Description = permission.Description,
                IsActive = permission.IsActive
            };
            return Ok(result);
        }
        // DELETE api/system/permissions/5
        [HttpDelete("{id:int}")]
        [RequirePermission("system:permissions:manage")]
        public async Task<IActionResult> DeletePermission(int id)
        {
            if (!IsPlatformAdmin())
            {
                _logger.LogWarning("DeletePermission: forbidden — caller lacks PlatformAdmin role");
                return Forbid();
            }
            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null)
            {
                _logger.LogWarning("DeletePermission: PermissionId={PermissionId} not found", id);
                return NotFound(new { message = "Permission not found." });
            }
            var isUsed = await _context.RolePermissions.AnyAsync(rp => rp.PermissionId == id);
            if (isUsed)
                return BadRequest(new { message = "Cannot delete permission because it is assigned to one or more roles." });
            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Permission deleted: PermissionId={PermissionId} Key={Key}", id, permission.Key);
            return Ok(new { message = "Permission deleted.", permissionId = id });
        }
        [HttpPost("check")]
        public async Task<IActionResult> CheckPermission([FromBody] PermissionCheckRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Permission))
                return BadRequest(new { message = "Permission key is required." });
            var role = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                       ?? User.FindFirst(ClaimTypes.Role)?.Value;
            var canonical = role switch
            {
                "SuperAdmin" => "PlatformAdmin",
                "Manager" => "AgencyAdmin",
                "Admin" => "AgencyAdmin",
                "Agent" => "Broker",
                _ => role
            };
            if (canonical == "PlatformAdmin")
                return Ok(new { granted = true });
            var roleDef = await _context.SystemRoles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Name == canonical && r.IsActive);
            var granted = roleDef != null && roleDef.RolePermissions.Any(rp =>
                rp.Permission != null && rp.Permission.IsActive && rp.Permission.Key == request.Permission);
            return Ok(new { granted });
        }
        private static string ParseModule(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            var parts = key.Split(':');
            return parts.Length > 0 ? parts[0] : string.Empty;
        }
        private static string ParseAction(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            var parts = key.Split(':');
            return parts.Length > 1 ? parts[1] : string.Empty;
        }
    }
}
