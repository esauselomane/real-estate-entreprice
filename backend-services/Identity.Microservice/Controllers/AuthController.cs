using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Identity.Microservice.Services;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Microservice.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IdentityDbContext _context;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IdentityDbContext context, TokenService tokenService, ILogger<AuthController> logger)
        {
            _context = context;
            _tokenService = tokenService;
            _logger = logger;
        }

        // --- 📥 TRANSFER DATA STRUCUTRE MODEL BINDINGS ---
                                 
         
                                                             
                                                                
         

                                          
         
                                                             
         
        // --- 🔐 ENDPOINT A: MULTI-TENANT SECURE AUTHENTICATION ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);

            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.AccountStatus == "Active");

            if (user == null)
            {
                _logger.LogWarning("Login failed: no active user found for email: {Email}", request.Email);
                return Unauthorized(new { message = "Invalid credential parameters provided." });
            }

            if (user.PasswordHash != request.Password)
            {
                _logger.LogWarning("Login failed: password mismatch for user: {UserId} ({Email})", user.UserId, user.Email);
                return Unauthorized(new { message = "Invalid credential parameters provided." });
            }

            _logger.LogInformation("Login successful for user: {UserId} ({Email}) Role: {Role} Tenant: {TenantId}",
                user.UserId, user.Email, user.SystemRole, user.TenantId);

            var permissions = await _context.UserAccessClaims
                .FirstOrDefaultAsync(p => p.UserId == user.UserId);

            if (permissions == null)
            {
                _logger.LogWarning("No UserAccessClaims found for user: {UserId}, using defaults", user.UserId);
                permissions = new UserAccessClaim { UserId = user.UserId };
            }

            // 4. Generate the secure cryptographic token carrying TenantId, Role, and Claims
            var tokenString = _tokenService.GenerateAccessToken(user, permissions);

            // 5. Send back authorization envelope package payload
            return Ok(new
            {
                token = tokenString,
                profile = new {
                    id = user.UserId,
                    tenantId = user.TenantId,
                    name = user.FullName,
                    email = user.Email,
                    role = user.SystemRole,
                    avatar = user.AvatarUrl
                }
            });
        }
        // --- 🏢 ENDPOINT B: ROLE-RESTRICTED SYSTEM USER PROVISIONING ---
        [Authorize]
        [HttpPost("register-user")]
        public async Task<IActionResult> RegisterUser([FromBody] UserAccount newUser)
        {
            _logger.LogInformation("User registration requested: Email={Email} Role={Role} Tenant={TenantId} By={CallerUserId}",
                newUser.Email, newUser.SystemRole, newUser.TenantId, User?.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var roleDef = await _context.SystemRoles
                .FirstOrDefaultAsync(r => r.Name == newUser.SystemRole && r.IsActive);
            if (roleDef == null)
            {
                _logger.LogWarning("Registration rejected: unsupported role '{Role}' for email {Email}", newUser.SystemRole, newUser.Email);
                return BadRequest(new { message = "Unsupported account role." });
            }

            var callerRole = GetCurrentCanonicalRole();
            if (callerRole == null) return Forbid();

            bool isPermittedCreator;
            if (callerRole == "PlatformAdmin")
                isPermittedCreator = true;           // Platform admin may provision ANY role, including AgencyAdmin
            else if (callerRole == "AgencyAdmin" && roleDef.IsAgencyScoped)
                isPermittedCreator = true;            // Agency admin may only provision agency-scoped roles (Broker/Owner/Tenant)
            else
                isPermittedCreator = false;

            if (!isPermittedCreator)
                return Forbid();

            // Tenant isolation: an agency admin may only provision users into its own
            // tenant; a platform admin may provision into any tenant.
            var callerTenantId = User.FindFirst("TenantId")?.Value;
            if (callerRole == "AgencyAdmin" && newUser.TenantId != callerTenantId)
                return Forbid();

            // 1. Verify that email structures do not collide inside the relational database engine
            var exists = await _context.UserAccounts.AnyAsync(u => u.Email == newUser.Email);
            if (exists)
            {
                return BadRequest(new { message = "Email signature already registered inside system clusters." });
            }

            // 2. Initialize default safe tracking keys and parameters
            if (string.IsNullOrEmpty(newUser.UserId))
            {
                newUser.UserId = "USR-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            }
            newUser.CreatedAt = DateTime.UtcNow;
            newUser.AccountStatus = "Active";

            // 3. Commit profile record block directly to the transactional pool
            _context.UserAccounts.Add(newUser);

            // 4. Instantiate default granular access claims matching their target primary role
            var defaultClaims = new UserAccessClaim
            {
                UserId = newUser.UserId,
                CanDeleteProperties = newUser.SystemRole == "PlatformAdmin",
                CanExportFinancialReports = true,
                CanBypassValuationStages = newUser.SystemRole == "PlatformAdmin"
            };
            _context.UserAccessClaims.Add(defaultClaims);

            await _context.SaveChangesAsync();

            _logger.LogInformation("User registered: UserId={UserId} Email={Email} Role={Role} Tenant={TenantId}",
                newUser.UserId, newUser.Email, newUser.SystemRole, newUser.TenantId);

            return Ok(new { 
                message = "New multi-tenant identity profile successfully provisioned.",
                userId = newUser.UserId,
                boundTenantId = newUser.TenantId
            });
        }

        // --- 📨 ENDPOINT C: SELF-SERVICE PASSWORD RECOVERY RECONCILIATION ---
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            _logger.LogInformation("Password reset requested for email: {Email}", request.Email);

            var userExists = await _context.UserAccounts.AnyAsync(u => u.Email == request.Email);
            
            if (!userExists)
            {
                _logger.LogWarning("Password reset: no active account for email: {Email}", request.Email);
                return Ok(new { message = "If the account path matches an active profile, a recovery reset link has been dispatched." });
            }

            _logger.LogInformation("Password reset token generated for email: {Email}", request.Email);
            var recoveryToken = Guid.NewGuid().ToString();
            
            // In a production build, this token would be stored in the database with an expiration time
            // and sent out via a real SMTP email client service wrapper.
            Console.WriteLine($"[EMAIL SIMULATION LOG]: Password reset token [{recoveryToken}] issued to {request.Email}");

            return Ok(new { message = "If the account path matches an active profile, a recovery reset link has been dispatched." });
        }

                                          
         
                                                                       
                                                                   
         

        // --- 🔐 ENDPOINT: SELF-SERVICE PASSWORD CHANGE (any authenticated user, own account only) ---
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { message = "Caller identity unavailable." });

            var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound(new { message = "Caller account not found." });

            // Plain-text verify (matches the local-testing login comparison strategy).
            if (user.PasswordHash != request.CurrentPassword)
                return Unauthorized(new { message = "Current password is incorrect." });

            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 6)
                return BadRequest(new { message = "New password must be at least 6 characters." });

            user.PasswordHash = request.NewPassword;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Password updated successfully." });
        }

        // --- 🚫 ENDPOINT: Disable a user account (AgencyAdmin within tenant, PlatformAdmin across tenants) ---
        [Authorize]
        [HttpPut("users/{userId}/disable")]
        public async Task<IActionResult> DisableUser(string userId)
        {
            var callerRole = GetCurrentCanonicalRole();
            var callerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (callerRole == null)
            {
                _logger.LogWarning("DisableUser: forbidden — caller {CallerUserId} has no resolvable role", callerUserId);
                return Forbid();
            }

            if (callerRole != "AgencyAdmin" && callerRole != "PlatformAdmin")
            {
                _logger.LogWarning("DisableUser: forbidden — caller {CallerUserId} role {CallerRole} cannot disable users", callerUserId, callerRole);
                return Forbid();
            }

            var callerTenantId = User.FindFirst("TenantId")?.Value;
            var callerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var target = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId);
            if (target == null) return NotFound(new { message = "Target account not found." });

            // An agency admin can only disable users inside their own tenant.
            if (callerRole == "AgencyAdmin" && target.TenantId != callerTenantId) return Forbid();

            // Never disable yourself, and never disable an administrative peer/superior.
            if (target.UserId == callerUserId) return BadRequest(new { message = "You cannot disable your own account." });
            var targetCanonical = target.SystemRole switch
            {
                "SuperAdmin" => "PlatformAdmin",
                "Manager" => "AgencyAdmin",
                "Admin" => "AgencyAdmin",
                "Agent" => "Broker",
                _ => target.SystemRole
            };
            if (targetCanonical == "PlatformAdmin" || targetCanonical == "AgencyAdmin")
                return Forbid();

            target.AccountStatus = "Disabled";
            await _context.SaveChangesAsync();
            _logger.LogInformation("User disabled: TargetUserId={TargetUserId} TargetEmail={TargetEmail} By={CallerUserId}",
                target.UserId, target.Email, callerUserId);
            return Ok(new { message = $"Account {target.Email} disabled successfully." });
        }

        // --- 🏢 ENDPOINT: List user accounts for role assignment (PlatformAdmin: all tenants; AgencyAdmin: own tenant)
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var callerRole = GetCurrentCanonicalRole();
            var callerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (callerRole == null)
            {
                _logger.LogWarning("GetUsers: forbidden — caller {CallerUserId} has no resolvable role", callerUserId);
                return Forbid();
            }
            if (callerRole != "PlatformAdmin" && callerRole != "AgencyAdmin")
            {
                _logger.LogWarning("GetUsers: forbidden — caller {CallerUserId} role {CallerRole}", callerUserId, callerRole);
                return Forbid();
            }

            var callerTenantId = User.FindFirst("TenantId")?.Value;
            if (callerRole == "PlatformAdmin")
                _logger.LogInformation("GetUsers: listing all tenant users by {CallerUserId}", callerUserId);
            else
                _logger.LogInformation("GetUsers: listing tenant {TenantId} users by {CallerUserId}", callerTenantId, callerUserId);

            var users = callerRole == "PlatformAdmin"
                ? await _context.UserAccounts.ToListAsync()
                : await _context.UserAccounts.Where(u => u.TenantId == callerTenantId).ToListAsync();

            return Ok(users.Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.TenantId,
                u.SystemRole,
                u.AccountStatus
            }));
        }

                                          
         
                                                                  
         

        // --- 🔧 ENDPOINT: Assign / change (add or remove) a role on a user account.
        // Role changes run through the same data-driven matrix as RegisterUser, so an
        // AgencyAdmin can only down-scope to agency-scoped roles within its own tenant.
        [HttpPut("users/{userId}/role")]
        public async Task<IActionResult> UpdateUserRole(string userId, [FromBody] UpdateUserRoleRequest request)
        {
            var callerRole = GetCurrentCanonicalRole();
            if (callerRole == null) return Forbid();
            if (callerRole != "PlatformAdmin" && callerRole != "AgencyAdmin") return Forbid();

            var callerTenantId = User.FindFirst("TenantId")?.Value;
            var callerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var target = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == userId);
            if (target == null) return NotFound(new { message = "Target account not found." });

            if (callerRole == "AgencyAdmin" && target.TenantId != callerTenantId) return Forbid();

            var roleDef = await _context.SystemRoles.FirstOrDefaultAsync(r => r.Name == request.SystemRole && r.IsActive);
            if (roleDef == null) return BadRequest(new { message = "Unsupported account role." });

            bool permitted = callerRole == "PlatformAdmin" || (callerRole == "AgencyAdmin" && roleDef.IsAgencyScoped);
            if (!permitted) return Forbid();

            if (target.UserId == callerUserId) return BadRequest(new { message = "You cannot change your own role." });

            _logger.LogInformation("User role updated: TargetUserId={TargetUserId} NewRole={NewRole} By={CallerUserId}",
                target.UserId, request.SystemRole, callerUserId);
            target.SystemRole = request.SystemRole;
            await _context.SaveChangesAsync();
            return Ok(new { message = "User role updated.", userId = target.UserId, systemRole = target.SystemRole });
        }

        // 🛡️ Resolve the caller's canonical role, normalizing the legacy seed aliases
        // (SuperAdmin/Manager/Agent/Admin) to their contract equivalents so role-gated
        // logic above stays consistent regardless of which seed populated the account.
        private string? GetCurrentCanonicalRole()
        {
            var role = User.FindFirst(c =>
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                ?? User.FindFirst(ClaimTypes.Role)?.Value;
            return role switch
            {
                "SuperAdmin" => "PlatformAdmin",
                "Manager" => "AgencyAdmin",
                "Admin" => "AgencyAdmin",
                "Agent" => "Broker",
                _ => role
            };
        }
    }
}
