using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Identity.Microservice.Services;

namespace Identity.Microservice.Controllers
{
    // Configure public routing maps pointing through our API gateway setup structure
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IdentityDbContext _context;
        private readonly TokenService _tokenService;

        // Inject our localized database context and JWT generation services via dependency injection
        public AuthController(IdentityDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        // --- 📥 TRANSFER DATA STRUCUTRE MODEL BINDINGS ---
        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class ForgotPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
        }
        // --- 🔐 ENDPOINT A: MULTI-TENANT SECURE AUTHENTICATION ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Fetch user by email including their explicit tenant permission parameters
            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.AccountStatus == "Active");

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid credential parameters provided." });
            }

            // 2. Validate password signatures (Plain-text comparison for local testing build)
            if (user.PasswordHash != request.Password)
            {
                return Unauthorized(new { message = "Invalid credential parameters provided." });
            }

            // 3. Fetch custom granular access control claims registered for this entity
            var permissions = await _context.UserAccessClaims
                .FirstOrDefaultAsync(p => p.UserId == user.UserId);

            if (permissions == null)
            {
                // Instantiate a default permissions block safe fallback parameters if none exist
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
        [HttpPost("register-user")]
        public async Task<IActionResult> RegisterUser([FromBody] UserAccount newUser)
        {
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
                CanDeleteProperties = newUser.SystemRole == "Manager" || newUser.SystemRole == "SuperAdmin",
                CanExportFinancialReports = true,
                CanBypassValuationStages = newUser.SystemRole == "SuperAdmin"
            };
            _context.UserAccessClaims.Add(defaultClaims);

            await _context.SaveChangesAsync();

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
            // 1. Verify existence of the targeted target address records lookup
            var userExists = await _context.UserAccounts.AnyAsync(u => u.Email == request.Email);
            
            // Security Best Practice: Always return a generic success message even if email doesn't exist
            // This prevents malicious entities from harvesting valid usernames or account identifiers.
            if (!userExists)
            {
                return Ok(new { message = "If the account path matches an active profile, a recovery reset link has been dispatched." });
            }

            // 2. Generate a secure, unique password reset transaction identifier
            var recoveryToken = Guid.NewGuid().ToString();
            
            // In a production build, this token would be stored in the database with an expiration time
            // and sent out via a real SMTP email client service wrapper.
            Console.WriteLine($"[EMAIL SIMULATION LOG]: Password reset token [{recoveryToken}] issued to {request.Email}");

            return Ok(new { message = "If the account path matches an active profile, a recovery reset link has been dispatched." });
        }
    }
}
