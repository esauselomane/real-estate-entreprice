using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions;
using System.Security.Claims;

namespace Identity.Microservice.Controllers
{
    /// <summary>
    /// Self-admin endpoints for the currently authenticated user — profile
    /// details, out-of-office toggle, and profile picture upload.
    /// User management (listing, role changes, disabling) stays in
    /// AuthController under /api/auth.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly IdentityDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IdentityDbContext dbContext,
            IWebHostEnvironment environment,
            ILogger<UserController> logger)
        {
            _dbContext = dbContext;
            _environment = environment;
            _logger = logger;
        }

        private string GetCurrentUserId()
            => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty;

        // --- 👤 GET current user's self-admin profile ---
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Caller identity unavailable." });

            var user = await _dbContext.UserAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "User account not found." });

             return Ok(new UserProfileResponse
             {
                 UserId = user.UserId,
                 FullName = user.FullName,
                 Email = user.Email,
                 SystemRole = user.SystemRole,
                 TenantId = user.TenantId,
                 AvatarUrl = user.AvatarUrl,
                 LicenseNumber = user.LicenseNumber,
                 LicenseExpiry = user.LicenseExpiry,
                 OutOfOffice = user.OutOfOffice,
                 EpilepsySafeMode = user.EpilepsySafeMode,
                 AccountStatus = user.AccountStatus
             });
        }

        // --- ✏️ PUT update self-admin profile (OOO toggle, display name) ---
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Caller identity unavailable." });

            var user = await _dbContext.UserAccounts
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "User account not found." });

            if (request.FullName != null)
            {
                if (string.IsNullOrWhiteSpace(request.FullName)) return BadRequest(new { message = "Full name is required." });
                user.FullName = request.FullName.Trim();
            }
            if (request.LicenseNumber != null) user.LicenseNumber = request.LicenseNumber.Trim();

             if (request.OutOfOffice.HasValue)
                 user.OutOfOffice = request.OutOfOffice.Value;

            if (request.EpilepsySafeMode.HasValue)
                user.EpilepsySafeMode = request.EpilepsySafeMode.Value;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Profile updated for UserId={UserId} OutOfOffice={OutOfOffice} EpilepsySafeMode={EpilepsySafeMode}",
                userId, user.OutOfOffice, user.EpilepsySafeMode);

            return Ok(new
            {
                message = "Profile updated successfully.",
                fullName = user.FullName,
                outOfOffice = user.OutOfOffice,
                epilepsySafeMode = user.EpilepsySafeMode
            });
        }

        // --- 📷 POST upload profile picture (round crop handled client-side) ---
        [HttpPost("profile/picture")]
        public async Task<IActionResult> UploadProfilePicture()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Caller identity unavailable." });

            var form = await Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided." });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = "Only image files (jpeg, png, webp, gif) are allowed." });

            if (file.Length > 5 * 1024 * 1024) return BadRequest(new { message = "Profile pictures must be no larger than 5 MB." });
            var uploadDir = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "profiles");
            Directory.CreateDirectory(uploadDir);

            var fileExtension = file.ContentType.ToLowerInvariant() switch
            {
                "image/png" => ".png", "image/webp" => ".webp", "image/gif" => ".gif", _ => ".jpg"
            };
            var fileName = $"{Guid.NewGuid():N}{fileExtension}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativeUrl = $"/profiles/{fileName}";

            var user = await _dbContext.UserAccounts
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "User account not found." });

            user.AvatarUrl = relativeUrl;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Profile picture uploaded for UserId={UserId} Url={Url}",
                userId, relativeUrl);

            return Ok(new
            {
                message = "Profile picture uploaded.",
                avatarUrl = relativeUrl
            });
        }
    }
}
