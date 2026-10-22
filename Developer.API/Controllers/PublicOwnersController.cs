using Developer.API.Data;
using Developer.API.DTOs;
using Developer.API.Models;
using Developer.API.Services.PublicListings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Developer.API.Controllers;

[ApiController]
[Route("api/developer/owners")]
[EnableRateLimiting("owner-auth")]
public sealed class PublicOwnersController(DeveloperDbContext database, OwnerSessionService sessions) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(OwnerCredentials request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || !new PhoneAttribute().IsValid(request.Phone) || string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "Your name and a valid contact number are required." });
        var agencyId = (string)HttpContext.Items["AgencyId"]!;
        var email = request.Email.Trim().ToUpperInvariant();
        if (await database.PublicOwnerAccounts.AnyAsync(owner => owner.AgencyId == agencyId && owner.Email == email, cancellationToken))
            return Conflict(new { message = "An account already exists. Please sign in." });
        var account = new PublicOwnerAccount { AgencyId = agencyId, Email = email, Name = request.Name.Trim(), Phone = request.Phone.Trim() };
        account.PasswordHash = new PasswordHasher<PublicOwnerAccount>().HashPassword(account, request.Password);
        database.PublicOwnerAccounts.Add(account);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Conflict(new { message = "Unable to create this account. Please sign in or try again." }); }
        var token = await sessions.CreateAsync(account, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token, account.Name, email = account.Email, account.Phone });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(OwnerCredentials request, CancellationToken cancellationToken)
    {
        var agencyId = (string)HttpContext.Items["AgencyId"]!;
        var email = request.Email.Trim().ToUpperInvariant();
        var account = await database.PublicOwnerAccounts.FirstOrDefaultAsync(owner => owner.AgencyId == agencyId && owner.Email == email, cancellationToken);
        if (account is null || new PasswordHasher<PublicOwnerAccount>().VerifyHashedPassword(account, account.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Email or password is incorrect." });
        var token = await sessions.CreateAsync(account, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token, account.Name, email = account.Email, account.Phone });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var agencyId = (string)HttpContext.Items["AgencyId"]!;
        var hash = ApiKeyGenerator.HashKey(Request.Headers["X-Owner-Token"].ToString());
        await database.PublicOwnerSessions.Where(item => item.Id == hash && item.AgencyId == agencyId).ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }
}
