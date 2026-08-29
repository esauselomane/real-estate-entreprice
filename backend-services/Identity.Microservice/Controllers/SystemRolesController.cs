using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using System.Security.Claims;

namespace Identity.Microservice.Controllers
{
    // 🔐 System-wide role definitions — the configuration surface exposed by the
    // System Admin "Roles" tab. Read by RegisterUser so the provisioning authority
    // matrix is fully data-driven instead of hard-coded.
    [ApiController]
    [Route("api/system/roles")]
    [Authorize]
    public class SystemRolesController : ControllerBase
    {
        private readonly IdentityDbContext _context;

        public SystemRolesController(IdentityDbContext context)
        {
            _context = context;
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

        // GET api/system/roles  — list all role definitions (read for the config grid)
        [HttpGet]
        public async Task<IActionResult> GetSystemRoles()
        {
            var roles = await _context.SystemRoles
                .OrderByDescending(r => r.IsPlatformScoped)
                .ThenBy(r => r.Name)
                .ToListAsync();
            return Ok(roles);
        }

        // GET api/system/roles/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSystemRole(int id)
        {
            var role = await _context.SystemRoles.FindAsync(id);
            if (role == null) return NotFound(new { message = "Role definition not found." });
            return Ok(role);
        }

        // POST api/system/roles  — create a new role definition (PlatformAdmin only)
        [HttpPost]
        public async Task<IActionResult> CreateSystemRole([FromBody] RoleDefinitionRequest request)
        {
            if (!IsPlatformAdmin()) return Forbid();

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
                PermissionsJson = request.PermissionsJson,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.SystemRoles.Add(role);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Role definition created.", roleId = role.RoleDefId });
        }

        // PUT api/system/roles/5  — update a role definition (PlatformAdmin only)
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateSystemRole(int id, [FromBody] RoleDefinitionRequest request)
        {
            if (!IsPlatformAdmin()) return Forbid();

            var role = await _context.SystemRoles.FindAsync(id);
            if (role == null) return NotFound(new { message = "Role definition not found." });

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
            role.PermissionsJson = request.PermissionsJson;
            role.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Role definition updated.", roleId = role.RoleDefId });
        }

        // DELETE api/system/roles/5  — deactivate a role definition (PlatformAdmin only, non-destructive)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSystemRole(int id)
        {
            if (!IsPlatformAdmin()) return Forbid();

            var role = await _context.SystemRoles.FindAsync(id);
            if (role == null) return NotFound(new { message = "Role definition not found." });

            // Reserved roles that drive the provisioning matrix cannot be deactivated.
            var reserved = new[] { "PlatformAdmin", "AgencyAdmin", "Broker", "Owner", "Tenant" };
            if (reserved.Contains(role.Name))
                return BadRequest(new { message = "Reserved role definitions cannot be deactivated." });

            role.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Role definition deactivated.", roleId = role.RoleDefId });
        }
    }
}
