using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using System.Security.Claims;
namespace Identity.Microservice.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IdentityDbContext _context;
        public PermissionHandler(IdentityDbContext context)
        {
            _context = context;
        }
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User == null || !context.User.Identity.IsAuthenticated)
            {
                context.Fail();
                return;
            }
            var userId = context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                         ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                context.Fail();
                return;
            }
            var userRole = context.User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                           ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
            // PlatformAdmin bypasses all permission checks
            var canonicalRole = userRole switch
            {
                "SuperAdmin" => "PlatformAdmin",
                "Manager" => "AgencyAdmin",
                "Admin" => "AgencyAdmin",
                "Agent" => "Broker",
                _ => userRole
            };
            if (canonicalRole == "PlatformAdmin")
            {
                context.Succeed(requirement);
                return;
            }
            // Get the user's role definition
            var roleDef = await _context.SystemRoles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Name == canonicalRole && r.IsActive);
            if (roleDef == null)
            {
                context.Fail();
                return;
            }
            // Check if the role has the required permission
            var hasPermission = roleDef.RolePermissions.Any(rp =>
                rp.Permission != null &&
                rp.Permission.IsActive &&
                rp.Permission.Key == requirement.Permission);
            if (hasPermission)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
    }
}
