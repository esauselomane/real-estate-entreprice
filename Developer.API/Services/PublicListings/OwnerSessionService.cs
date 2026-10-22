using Developer.API.Data;
using Developer.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Developer.API.Services.PublicListings;

/// <summary>Opaque owner sessions are agency-bound and stored only as hashes.</summary>
public sealed class OwnerSessionService(DeveloperDbContext database)
{
    public async Task<PublicOwnerAccount?> FindOwnerAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var agencyId = context.Items["AgencyId"] as string;
        var token = context.Request.Headers["X-Owner-Token"].ToString();
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = ApiKeyGenerator.HashKey(token);
        var session = await database.PublicOwnerSessions.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == hash && item.AgencyId == agencyId && item.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (session is null) return null;
        return await database.PublicOwnerAccounts.FirstOrDefaultAsync(item => item.Id == session.OwnerId && item.AgencyId == agencyId, cancellationToken);
    }

    public async Task<string> CreateAsync(PublicOwnerAccount owner, CancellationToken cancellationToken)
    {
        var rawToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        database.PublicOwnerSessions.Add(new PublicOwnerSession
        {
            Id = ApiKeyGenerator.HashKey(rawToken), OwnerId = owner.Id,
            AgencyId = owner.AgencyId, ExpiresAt = DateTime.UtcNow.AddHours(8)
        });
        await database.SaveChangesAsync(cancellationToken);
        return rawToken;
    }
}
