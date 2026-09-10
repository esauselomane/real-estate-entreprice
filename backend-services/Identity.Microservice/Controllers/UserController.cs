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
                user.FullName = request.FullName;

            if (request.OutOfOffice.HasValue)
                user.OutOfOffice = request.OutOfOffice.Value;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Profile updated for UserId={UserId} OutOfOffice={OutOfOffice}",
                userId, user.OutOfOffice);

            return Ok(new
            {
                message = "Profile updated successfully.",
                fullName = user.FullName,
                outOfOffice = user.OutOfOffice
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

            var uploadDir = Path.Combine(_environment.ContentRootPath, "..", "..", "..", "..", "Identity.Microservice", "wwwroot", "profiles");
            Directory.CreateDirectory(uploadDir);

            var fileExtension = Path.GetExtension(file.FileName) ?? ".jpg";
            var fileName = $"{userId}{fileExtension}";
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
