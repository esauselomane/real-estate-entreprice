using Identity.Microservice.Services.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions;
using Identity.Microservice.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Microservice.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IdentityDbContext _dbContext;
        private readonly IUserRepository _userRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly ISystemRoleRepository _systemRoleRepository;
        private readonly IUserAccessClaimRepository _userAccessClaimRepository;
        private readonly IRepository<LeadCaptureRequest> _leadCaptureRepository;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthController> _logger;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthController(IdentityDbContext dbContext, TokenService tokenService, ILogger<AuthController> logger, IEmailService emailService, IConfiguration configuration, IUserRepository userRepository, ITenantRepository tenantRepository, ISystemRoleRepository systemRoleRepository, IUserAccessClaimRepository userAccessClaimRepository, IRepository<LeadCaptureRequest> leadCaptureRepository)
        {
            _dbContext = dbContext;
            _userRepository = userRepository;
            _tenantRepository = tenantRepository;
            _systemRoleRepository = systemRoleRepository;
            _userAccessClaimRepository = userAccessClaimRepository;
            _leadCaptureRepository = leadCaptureRepository;
            _tokenService = tokenService;
            _logger = logger;
            _emailService = emailService;
            _configuration = configuration;
        }

         
        // --- 🔐 ENDPOINT A: MULTI-TENANT SECURE AUTHENTICATION ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);

            var user = await _userRepository.Query()
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.AccountStatus == "Active");

            if (user == null)
            {
                _logger.LogWarning("Login failed: no active user found for email: {Email}", request.Email);
                return Unauthorized(new { message = "Invalid credential parameters provided." });
            }

            if (!AccountPasswordHelper.Verify(user, request.Password))
            {
                _logger.LogWarning("Login failed: password mismatch for user: {UserId} ({Email})", user.UserId, user.Email);
                return Unauthorized(new { message = "Invalid credential parameters provided." });
            }

            // Expired trials must still authenticate to reach subscription recovery.
            _logger.LogInformation("Login successful for user: {UserId} ({Email}) Role: {Role} Tenant: {TenantId}",
                user.UserId, user.Email, user.SystemRole, user.TenantId);

            var permissions = await _userAccessClaimRepository.Query()
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
                    avatar = user.AvatarUrl,
                    isTrialActive = user.IsTrial && user.TrialEndDate.HasValue && DateTime.UtcNow <= user.TrialEndDate.Value,
                    trialExpiresAt = user.TrialEndDate?.ToString("o")
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

            var roleDef = await _systemRoleRepository.Query()
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
            else if (callerRole == "AgencyAdmin" && !roleDef.IsPlatformScoped && new[] { "AgencyAdmin", "Broker", "Owner", "Tenant" }.Contains(roleDef.Name))
                isPermittedCreator = true;            // Agency admin may only provision agency-scoped roles (Broker/Owner/Tenant)
            else
                isPermittedCreator = false;

            if (!isPermittedCreator)
                return Forbid();

            // Tenant isolation: an agency admin may only provision users into its own
            // tenant or a direct child agency under their subtree; a platform admin may
            // provision into any tenant.
            var callerTenantId = User.FindFirst("TenantId")?.Value;
            if (callerRole == "AgencyAdmin")
            {
                var allowedTenants = await GetAgencySubtreeTenantIdsAsync(callerTenantId);
                if (callerTenantId != null && !allowedTenants.Contains(callerTenantId))
                    allowedTenants.Add(callerTenantId);
                if (newUser.TenantId == null || !allowedTenants.Contains(newUser.TenantId))
                {
                    _logger.LogWarning("RegisterUser: AgencyAdmin {Caller} attempted to provision into tenant {Target} outside subtree {Allowed}",
                        callerTenantId, newUser.TenantId, string.Join(",", allowedTenants));
                    return Forbid();
                }
            }

            // 1. Verify that email structures do not collide inside the relational database engine
            var exists = await _userRepository.Query().AnyAsync(u => u.Email == newUser.Email);
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
            _userRepository.Add(newUser);

            // 4. Instantiate default granular access claims matching their target primary role
            var defaultClaims = new UserAccessClaim
            {
                UserId = newUser.UserId,
                CanDeleteProperties = newUser.SystemRole == "PlatformAdmin",
                CanExportFinancialReports = true,
                CanBypassValuationStages = newUser.SystemRole == "PlatformAdmin"
            };
            _userAccessClaimRepository.Add(defaultClaims);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("User registered: UserId={UserId} Email={Email} Role={Role} Tenant={TenantId}",
                newUser.UserId, newUser.Email, newUser.SystemRole, newUser.TenantId);

            return Ok(new { 
                message = "New multi-tenant identity profile successfully provisioned.",
                userId = newUser.UserId,
                boundTenantId = newUser.TenantId
            });
        }

        [Authorize]
        [HttpGet("tenant-roster")]
        public async Task<IActionResult> GetTenantRoster()
        {
            if (GetCurrentCanonicalRole() != "PlatformAdmin") return Forbid();
            var tenants = await _tenantRepository.Query()
                .OrderBy(tenant => tenant.AgencyName)
                .Select(tenant => new { tenant.TenantId, Name = tenant.AgencyName })
                .ToListAsync();
            return Ok(tenants);
        }

        [Authorize]
        [HttpGet("visible-tenants")]
        public async Task<IActionResult> GetVisibleTenants()
        {
            var tenantId = User.FindFirst("TenantId")?.Value;
            if (GetCurrentCanonicalRole() == "AgencyAdmin")
                return Ok(await GetAgencySubtreeTenantIdsAsync(tenantId));
            return Ok(string.IsNullOrWhiteSpace(tenantId) ? new List<string>() : new List<string> { tenantId });
        }

        [Authorize]
        [HttpPost("agency-tenants")]
        public async Task<IActionResult> CreateAgencyTenant(CreateAgencyTenantRequest request)
        {
            var role = GetCurrentCanonicalRole();
            if (role is not ("AgencyAdmin" or "PlatformAdmin")) return Forbid();
            var parentId = role == "AgencyAdmin" ? User.FindFirst("TenantId")?.Value : request.ParentTenantId;
            var parent = await _dbContext.TenantAgencies.FirstOrDefaultAsync(tenant => tenant.TenantId == parentId);
            if (role == "AgencyAdmin" && parent == null) return Forbid();
            if (await _dbContext.TenantAgencies.AnyAsync(tenant => tenant.TenantId == request.TenantId)) return Conflict();
            var tenant = new TenantAgency
            {
                TenantId = request.TenantId, AgencyName = request.AgencyName, ContactEmail = request.ContactEmail,
                ParentTenantId = parentId, IsTrial = parent?.IsTrial ?? true,
                TrialStartDate = parent?.TrialStartDate ?? DateTime.UtcNow,
                TrialEndDate = parent?.TrialEndDate ?? DateTime.UtcNow.AddMonths(2)
            };
            _dbContext.TenantAgencies.Add(tenant);
            await _dbContext.SaveChangesAsync();
            return Ok(new { tenant.TenantId });
        }

        // --- 📨 ENDPOINT C: SELF-SERVICE PASSWORD RECOVERY RECONCILIATION ---
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request,
            [FromServices] PasswordRecoveryService recovery)
        {
            await recovery.RequestAsync(request.Email);
            return Ok(new { message = "If this email belongs to an active account, a password reset link will be sent." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request,
            [FromServices] PasswordRecoveryService recovery)
        {
            if (!await recovery.ResetAsync(request.Token, request.NewPassword))
                return BadRequest(new { message = "This password reset link is invalid or expired. Please request a new one." });
            return Ok(new { message = "Your password has been reset. You can now sign in." });
        }

        // --- 🔐 ENDPOINT: SELF-SERVICE PASSWORD CHANGE (any authenticated user, own account only) ---
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { message = "Caller identity unavailable." });

            var user = await _userRepository.Query().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound(new { message = "Caller account not found." });

            // Verify hashed credentials while supporting existing legacy accounts.
            if (!AccountPasswordHelper.Verify(user, request.CurrentPassword))
                return Unauthorized(new { message = "Current password is incorrect." });

            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 6)
                return BadRequest(new { message = "New password must be at least 6 characters." });

            user.PasswordHash = AccountPasswordHelper.Hash(user, request.NewPassword);
            await _dbContext.SaveChangesAsync();
            await _emailService.SendEmailAsync(user.Email, "Your password has been changed", "<p>Your account password was changed successfully.</p><p>If you did not make this change, contact your agency administrator immediately to secure your account.</p>");
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
            
            var target = await _userRepository.Query().FirstOrDefaultAsync(u => u.UserId == userId);
            if (target == null) return NotFound(new { message = "Target account not found." });

            // An agency admin can only disable users inside their own tenant.
            if (callerRole == "AgencyAdmin" && !(await GetAgencySubtreeTenantIdsAsync(callerTenantId)).Contains(target.TenantId)) return Forbid();

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
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("User disabled: TargetUserId={TargetUserId} TargetEmail={TargetEmail} By={CallerUserId}",
                target.UserId, target.Email, callerUserId);
            return Ok(new { message = $"Account {target.Email} disabled successfully." });
        }

        // --- 🏢 ENDPOINT: List user accounts for role assignment (PlatformAdmin: all tenants; AgencyAdmin: own tenant)
        //   Supports optional server-side pagination (?page=&pageSize=) and a case-insensitive
        //   substring search (?search=) across name, email, tenant, and system role.
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
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
                _logger.LogInformation("GetUsers: listing tenant users by {CallerUserId} (page {Page} size {PageSize} search '{Search}')", callerUserId, page, pageSize, search);
            else
                _logger.LogInformation("GetUsers: listing tenant {TenantId} users by {CallerUserId} (page {Page} size {PageSize} search '{Search}')", callerTenantId, callerUserId, page, pageSize, search);

            if (page < 1) page = 1;
            var effectivePageSize = Math.Clamp(pageSize, 1, 100);

            // The query is built once and narrowed by tenant scope, then by the optional
            // search term, before applying a deterministic order + paging window so the
            // page boundaries stay stable as the roster changes.
            var query = _userRepository.Query();
            if (callerRole != "PlatformAdmin")
            {
                var visibleTenants = await GetAgencySubtreeTenantIdsAsync(callerTenantId);
                query = query.Where(u => visibleTenants.Contains(u.TenantId));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim();
                query = query.Where(u =>
                    u.FullName.Contains(normalizedSearch) ||
                    u.Email.Contains(normalizedSearch) ||
                    u.TenantId.Contains(normalizedSearch) ||
                    u.SystemRole.Contains(normalizedSearch));
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.UserId)
                .Skip((page - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)effectivePageSize);

            return Ok(new
            {
                items = users.Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.TenantId,
                    u.SystemRole,
                    u.AccountStatus
                }).ToArray(),
                totalCount,
                page,
                pageSize = effectivePageSize,
                totalPages,
                hasPrevious = page > 1,
                hasNext = page < totalPages
            });
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
            var target = await _userRepository.Query().FirstOrDefaultAsync(u => u.UserId == userId);
            if (target == null) return NotFound(new { message = "Target account not found." });

            if (callerRole == "AgencyAdmin" && !(await GetAgencySubtreeTenantIdsAsync(callerTenantId)).Contains(target.TenantId)) return Forbid();

            if (callerRole == "AgencyAdmin" && target.SystemRole is "PlatformAdmin" or "SuperAdmin") return Forbid();

            var roleDef = await _systemRoleRepository.Query().FirstOrDefaultAsync(r => r.Name == request.SystemRole && r.IsActive);
            if (roleDef == null) return BadRequest(new { message = "Unsupported account role." });

            bool permitted = callerRole == "PlatformAdmin" || (callerRole == "AgencyAdmin" && !roleDef.IsPlatformScoped && new[] { "AgencyAdmin", "Broker", "Owner", "Tenant" }.Contains(roleDef.Name));
            if (!permitted) return Forbid();

            if (target.UserId == callerUserId) return BadRequest(new { message = "You cannot change your own role." });

            _logger.LogInformation("User role updated: TargetUserId={TargetUserId} NewRole={NewRole} By={CallerUserId}",
                target.UserId, request.SystemRole, callerUserId);
            target.SystemRole = request.SystemRole;
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "User role updated.", userId = target.UserId, systemRole = target.SystemRole });
        }

        // --- 🔐 ENDPOINT: Admin reset of a user's password (no current password required).
        //   A PlatformAdmin may reset any user's password; an AgencyAdmin may only reset
        //   users within their agency subtree. The new credential is hashed with the same
        //   AccountPasswordHelper used at login, so it never fails the verification path.
        [Authorize]
        [HttpPut("users/{userId}/password")]
        public async Task<IActionResult> ResetUserPassword(string userId, [FromBody] UserPasswordUpdateRequest request)
        {
            var callerRole = GetCurrentCanonicalRole();
            if (callerRole == null || (callerRole != "PlatformAdmin" && callerRole != "AgencyAdmin"))
            {
                _logger.LogWarning("ResetUserPassword: forbidden — caller role {CallerRole} cannot reset passwords", callerRole);
                return Forbid();
            }

            var callerTenantId = User.FindFirst("TenantId")?.Value;
            var callerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var target = await _userRepository.Query().FirstOrDefaultAsync(u => u.UserId == userId);
            if (target == null) return NotFound(new { message = "Target account not found." });

            // An agency admin may only reset passwords for users inside their subtree.
            if (callerRole == "AgencyAdmin" && !(await GetAgencySubtreeTenantIdsAsync(callerTenantId)).Contains(target.TenantId))
                return Forbid();

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return BadRequest(new { message = "New password must be at least 6 characters." });

            try
            {
                // Hash with the same helper used at login so the new password is immediately valid.
                target.PasswordHash = AccountPasswordHelper.Hash(target, request.NewPassword);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Password reset by {CallerUserId} ({CallerRole}) for target {TargetUserId} ({TargetEmail})",
                    callerUserId, callerRole, target.UserId, target.Email);

                return Ok(new { message = "Password updated successfully.", userId = target.UserId });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Password reset failed for target {TargetUserId} by {CallerUserId}", target.UserId, callerUserId);
                return StatusCode(500, new { message = "Password could not be updated. Please try again." });
            }
        }

        // --- 🎯 ENDPOINT: TRIAL ONBOARDING — create agency + pending admin user, send activation email ---
        [HttpPost("trial/onboarding")]
        [AllowAnonymous]
        public async Task<IActionResult> TrialOnboarding([FromBody] TrialOnboardingRequest request)
        {
            _logger.LogInformation("Trial onboarding requested for email: {Email} Agency: {AgencyName}",
                request.AdminEmail, request.AgencyName);

            if (string.IsNullOrWhiteSpace(request.AgencyName) || string.IsNullOrWhiteSpace(request.AdminEmail) || string.IsNullOrWhiteSpace(request.AdminFullName))
            {
                _logger.LogWarning("Trial onboarding rejected: missing required fields");
                return BadRequest(new { message = "Company name, admin full name, and admin email are required." });
            }

            // Check for duplicate email
            var existingUser = await _userRepository.Query().AnyAsync(u => u.Email == request.AdminEmail);
            if (existingUser)
            {
                _logger.LogWarning("Trial onboarding: email already registered: {Email}", request.AdminEmail);
                return Ok(new { message = "If that email address is available, we have sent a verification link to complete your registration. Please check your inbox and spam folder." });
            }

            var lead = new LeadCaptureRequest
            {
                FullName = request.AdminFullName,
                Email = request.AdminEmail,
                Phone = request.AdminPhone,
                Company = request.AgencyName,
                UserType = LeadUserType.Agency,
                Source = "onboarding",
                Message = "Trial agency onboarding request",
                Status = LeadStatus.New
            };
            _leadCaptureRepository.Add(lead);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Trial onboarding lead could not be saved for email: {Email}", request.AdminEmail);
                return StatusCode(502, new { message = "Your details could not be saved. Please try again." });
            }
            _logger.LogInformation("Trial onboarding lead saved: LeadId={LeadId} Email={Email}", lead.Id, request.AdminEmail);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Generate a unique tenant ID for the new trial agency
                var tenantId = "TEN-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
                var activationToken = Guid.NewGuid().ToString("N");
                var trialStartDate = DateTime.UtcNow;
                var trialEndDate = trialStartDate.AddDays(14);

                // Create TenantAgency record (Identity DB)
                var tenant = new TenantAgency
                {
                    TenantId = tenantId,
                    AgencyName = request.AgencyName,
                    ContactEmail = request.AdminEmail,
                    HeadquartersAddress = request.HeadquartersAddress,
                    BrokerageLicense = request.BrokerageLicense,
                    OperationalStatus = "TrialPending",
                    IsTrial = true,
                    TrialExtensionAppliedAt = DateTime.UtcNow,
                    TrialStartDate = trialStartDate,
                    TrialEndDate = trialEndDate,
                    CreatedAt = DateTime.UtcNow
                };
                _tenantRepository.Add(tenant);

                // Create the pending admin user account
                var adminUser = new UserAccount
                {
                    UserId = "USR-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                    TenantId = tenantId,
                    FullName = request.AdminFullName,
                    Email = request.AdminEmail,
                    PasswordHash = string.Empty, // No password until activation
                    SystemRole = "AgencyAdmin", // Trial registration creates a tenant-scoped administrator
                    AccountStatus = "PendingActivation", // Cannot log in until password is set
                    IsTrial = true,
                    TrialStartDate = trialStartDate,
                    TrialEndDate = trialEndDate,
                    LicenseNumber = request.BrokerageLicense,
                    ActivationToken = activationToken,
                    ActivationTokenExpiry = trialStartDate.AddHours(48),
                    CreatedAt = DateTime.UtcNow
                };
                _userRepository.Add(adminUser);
                _userAccessClaimRepository.Add(new UserAccessClaim
                {
                    UserId = adminUser.UserId,
                    CanDeleteProperties = false,
                    CanExportFinancialReports = true,
                    CanBypassValuationStages = false
                });

                await _dbContext.SaveChangesAsync();

                // Send activation email with secure link
                var frontendUrl = _configuration.GetSection("Urls:FrontendClient").Get<string>() ?? $"{Request.Scheme}://{Request.Host}";
                var activationLink = $"{frontendUrl}/set-password?token={activationToken}";
                await _emailService.SendPasswordSetupEmailAsync(
                    request.AdminEmail, request.AdminFullName, activationLink, request.AgencyName);

                await transaction.CommitAsync();
                _logger.LogInformation("Trial onboarding complete: TenantId={TenantId} UserId={UserId} Email={Email} TrialEnds={TrialEndDate}",
                    tenantId, adminUser.UserId, request.AdminEmail, trialEndDate);

                return Ok(new
                {
                    message = "Trial agency created. Check your email to set your password and activate your account.",
                    tenantId = tenantId,
                    trialEndDate = trialEndDate,
                    adminEmail = request.AdminEmail
                });
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();
                _logger.LogError(exception, "Trial onboarding failed for email: {Email} Agency: {AgencyName}",
                    request.AdminEmail, request.AgencyName);
                return StatusCode(502, new
                {
                    message = "Trial agency could not be activated because the confirmation email could not be sent. Please try again."
                });
            }
        }

         // --- 🔐 ENDPOINT: SET PASSWORD via activation token — activates user and agency ---
         [HttpPost("trial/set-password")]
        [AllowAnonymous]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            _logger.LogInformation("SetPassword requested for token: {TokenPrefix}***",
                request.ActivationToken?.Substring(0, Math.Min(8, request.ActivationToken.Length)) ?? "null");

            if (string.IsNullOrWhiteSpace(request.ActivationToken))
            {
                return BadRequest(new { message = "Activation token is required." });
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Password must be at least 6 characters." });
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return BadRequest(new { message = "Passwords do not match." });
            }

            var user = await _userRepository.Query()
                .FirstOrDefaultAsync(u => u.ActivationToken == request.ActivationToken);

            if (user == null)
            {
                _logger.LogWarning("SetPassword: invalid activation token");
                return BadRequest(new { message = "Invalid or expired activation token." });
            }

            if (user.ActivationTokenExpiry.HasValue && DateTime.UtcNow > user.ActivationTokenExpiry.Value)
            {
                _logger.LogWarning("SetPassword: token expired for user: {UserId}", user.UserId);
                return BadRequest(new { message = "This activation link has expired. Please contact support." });
            }

            // Set the password and activate the account
            user.PasswordHash = AccountPasswordHelper.Hash(user, request.NewPassword);
            user.AccountStatus = "Active";
            user.ActivationToken = string.Empty;
            await _dbContext.SaveChangesAsync();

            // Activate the tenant agency
            var tenant = await _tenantRepository.Query().FirstOrDefaultAsync(t => t.TenantId == user.TenantId);
            if (tenant != null)
            {
                tenant.OperationalStatus = "Active";
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Password set and account activated: UserId={UserId} Email={Email} Tenant={TenantId}",
                user.UserId, user.Email, user.TenantId);

            return Ok(new
            {
                message = "Password set successfully. Your account is now active.",
                userId = user.UserId,
                tenantId = user.TenantId,
                role = user.SystemRole
            });
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

        // --- 📥 ENDPOINT: PLATFORM-ADMIN BATCH IMPORT (tenant + user seed data) ---
        // Accepts a multi-sheet payload keyed by sheet name ("TenantAgencies",
        // "UserAccounts"). Each sheet is a list of column-name → value rows.
        // Platform-admin only; rows are stamped with their own TenantId from the
        // document so a full tenant+user hierarchy can be seeded in one upload.
        [Authorize]
        [HttpPost("import")]
        public async Task<IActionResult> ImportBatch([FromBody] IdentityBatchImportRequest request)
        {
            if (GetCurrentCanonicalRole() != "PlatformAdmin")
            {
                _logger.LogWarning("ImportBatch rejected: caller role is not PlatformAdmin.");
                return Forbid();
            }

            var results = new Dictionary<string, IdentityBatchSheetResult>(StringComparer.OrdinalIgnoreCase);

            foreach (var sheetEntry in request.Sheets)
            {
                var errors = new List<string>();
                IdentityBatchSheetResult sheetResult;

                switch (sheetEntry.Key)
                {
                    case "TenantAgencies":
                        sheetResult = ImportTenantAgencies(sheetEntry.Value, errors);
                        break;
                    case "UserAccounts":
                        sheetResult = ImportUserAccounts(sheetEntry.Value, request.TenantId, errors);
                        break;
                    default:
                        sheetResult = new IdentityBatchSheetResult(0, 0, new List<string> { $"Unknown sheet '{sheetEntry.Key}' skipped." });
                        break;
                }

                results[sheetEntry.Key] = sheetResult;
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Identity batch import completed across {SheetCount} sheets.", request.Sheets.Count);

            return Ok(new IdentityBatchImportResponse { Sheets = results });
        }

        private IdentityBatchSheetResult ImportTenantAgencies(List<Dictionary<string, string>> rows, List<string> errors)
        {
            var imported = 0;
            foreach (var row in rows)
            {
                try
                {
                    var tenant = new TenantAgency
                    {
                        TenantId = row.GetValueOrDefault("TenantId") ?? string.Empty,
                        AgencyName = row.GetValueOrDefault("AgencyName") ?? string.Empty,
                        LogoInitials = row.GetValueOrDefault("LogoInitials") ?? string.Empty,
                        ContactEmail = row.GetValueOrDefault("ContactEmail") ?? string.Empty,
                        HeadquartersAddress = row.GetValueOrDefault("HeadquartersAddress") ?? string.Empty,
                        OperationalStatus = row.GetValueOrDefault("OperationalStatus") ?? "Active",
                        BrokerageLicense = row.GetValueOrDefault("BrokerageLicense") ?? string.Empty,
                        ParentTenantId = ParseNullableString(row.GetValueOrDefault("ParentTenantId")),
                        PeachCustomerId = ParseNullableString(row.GetValueOrDefault("PeachCustomerId")),
                        SubscriptionStatus = ParseSubscriptionStatus(row.GetValueOrDefault("SubscriptionStatus")),
                        SubscriptionEndsAt = ParseDateTime(row.GetValueOrDefault("SubscriptionEndsAt")),
                        ActiveSubscriptionId = ParseNullableInt(row.GetValueOrDefault("ActiveSubscriptionId")),
                        IsTrial = ParseBool(row.GetValueOrDefault("IsTrial")),
                        TrialStartDate = ParseDateTime(row.GetValueOrDefault("TrialStartDate")),
                        TrialEndDate = ParseDateTime(row.GetValueOrDefault("TrialEndDate")),
                        CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow
                    };

                    if (string.IsNullOrWhiteSpace(tenant.TenantId))
                    {
                        throw new InvalidOperationException("TenantId is required for TenantAgency import.");
                    }

                    _dbContext.TenantAgencies.Add(tenant);
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"TenantAgency row: {ex.Message}");
                }
            }

            return new IdentityBatchSheetResult(imported, errors.Count, errors);
        }

        private IdentityBatchSheetResult ImportUserAccounts(List<Dictionary<string, string>> rows, string fallbackTenantId, List<string> errors)
        {
            var imported = 0;
            foreach (var row in rows)
            {
                try
                {
                    var tenantId = row.GetValueOrDefault("TenantId") ?? fallbackTenantId;
                    var passwordHash = row.GetValueOrDefault("PasswordHash") ?? string.Empty;

                    var user = new UserAccount
                    {
                        UserId = row.GetValueOrDefault("UserId") ?? string.Empty,
                        TenantId = tenantId,
                        FullName = row.GetValueOrDefault("FullName") ?? string.Empty,
                        Email = row.GetValueOrDefault("Email") ?? string.Empty,
                        SystemRole = row.GetValueOrDefault("SystemRole") ?? "Broker",
                        AvatarUrl = row.GetValueOrDefault("AvatarUrl") ?? string.Empty,
                        AccountStatus = row.GetValueOrDefault("AccountStatus") ?? "Active",
                        LicenseNumber = row.GetValueOrDefault("LicenseNumber") ?? string.Empty,
                        LicenseExpiry = ParseDateTime(row.GetValueOrDefault("LicenseExpiry")),
                        IsTrial = ParseBool(row.GetValueOrDefault("IsTrial")),
                        TrialStartDate = ParseDateTime(row.GetValueOrDefault("TrialStartDate")),
                        TrialEndDate = ParseDateTime(row.GetValueOrDefault("TrialEndDate")),
                        CreatedAt = ParseDateTime(row.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow
                    };

                    if (string.IsNullOrWhiteSpace(user.UserId))
                    {
                        user.UserId = "USR-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                    }

                    if (string.IsNullOrWhiteSpace(passwordHash))
                    {
                        passwordHash = AccountPasswordHelper.Hash(user, "Password123!");
                    }
                    else if (!passwordHash.StartsWith("identity-v3:", StringComparison.Ordinal))
                    {
                        passwordHash = AccountPasswordHelper.Hash(user, passwordHash);
                    }

                    user.PasswordHash = passwordHash;

                    _dbContext.UserAccounts.Add(user);

                    _dbContext.UserAccessClaims.Add(new UserAccessClaim
                    {
                        UserId = user.UserId,
                        CanDeleteProperties = user.SystemRole == "PlatformAdmin" || user.SystemRole == "SuperAdmin",
                        CanExportFinancialReports = true,
                        CanBypassValuationStages = user.SystemRole == "PlatformAdmin" || user.SystemRole == "SuperAdmin"
                    });

                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"UserAccount row: {ex.Message}");
                }
            }

            return new IdentityBatchSheetResult(imported, errors.Count, errors);
        }

        private static string? ParseNullableString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static int ParseNullableInt(string? value)
        {
            return int.TryParse(value, out var result) ? result : 0;
        }

        private static bool ParseBool(string? value, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        private static DateTime? ParseDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return DateTime.TryParse(value, out var result) ? result : null;
        }

        private static SubscriptionStatus ParseSubscriptionStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return SubscriptionStatus.Trialing;

            return Enum.TryParse<SubscriptionStatus>(value, true, out var parsed) ? parsed : SubscriptionStatus.Trialing;
        }

        // 🌳 BFS over TenantAgencies.ParentTenantId — return all child tenant ids
        // reachable from the supplied root tenant. Used by AgencyAdmin provisioning
        // to restrict user-creation to their visible subtree.
        private async Task<List<string>> GetAgencySubtreeTenantIdsAsync(string? rootTenantId)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(rootTenantId)) return result;
            var allAgencies = await _tenantRepository.Query()
                .Select(t => new { t.TenantId, t.ParentTenantId })
                .ToListAsync();
            var byParent = allAgencies
                .Where(a => !string.IsNullOrWhiteSpace(a.ParentTenantId))
                .GroupBy(a => a.ParentTenantId!)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TenantId).ToList());
            return AgencyTenantScope.GetSubtree(rootTenantId, byParent);
        }
    }
}
