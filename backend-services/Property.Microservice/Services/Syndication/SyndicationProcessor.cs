using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.DTOs.Syndication;

namespace Property.Microservice.Services.Syndication;

/// <summary>Processes committed work under a database lock. Ambiguous failures require reconciliation,
/// never a blind create retry. A crash rolls back the job; adapters must use the stable request key.</summary>
public sealed class SyndicationProcessor(PropertyDbContext database, ListingProviderRegistry registry)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await SyndicationCoordinator.AcquireLockAsync(database, cancellationToken);
        var now = DateTime.UtcNow;
        var command = await database.SyndicationCommands.Where(item => item.State == "Pending" && item.AvailableAt <= now)
            .OrderBy(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (command == null) return false;
        var publication = await database.SyndicationPublications.SingleAsync(item => item.Id == command.PublicationId, cancellationToken);
        if (publication.Revision != command.Revision)
        {
            command.State = "Superseded";
        }
        else
        {
            var connection = await database.SyndicationConnections.SingleAsync(item => item.Id == publication.ConnectionId, cancellationToken);
            ProviderResult result;
            try
            {
                var provider = registry.Get(connection.ProviderKey);
                var snapshot = JsonSerializer.Deserialize<ListingSnapshot>(command.SnapshotJson)!;
                if (snapshot.TenantId != connection.TenantId || publication.TenantId != connection.TenantId)
                    throw new InvalidOperationException("Publication agency mismatch.");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                if ((!connection.Enabled || !connection.HasApiKey) && command.Operation == "Publish") result = new ProviderResult("NeedsAttention", Error: "The portal connection is disabled.");
                else result = await provider.ExecuteAsync(command.Operation, snapshot, connection, publication.ExternalId, command.RequestKey, timeout.Token);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                result = new ProviderResult("NeedsAttention", Error: "The provider outcome could not be confirmed. Check the portal before retrying.");
            }
            command.Attempts++;
            command.Error = result.Error;
            command.State = "Completed";
            publication.State = result.State;
            publication.Error = result.Error;
            publication.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(result.ExternalId)) publication.ExternalId = result.ExternalId;
            if (Uri.TryCreate(result.PublicUrl, UriKind.Absolute, out var url) && url.Scheme == "https") publication.PublicUrl = url.AbsoluteUri;
            if (result.State == "Live" || result.State == "Withdrawn" || result.State == "NotPublished")
            {
                publication.ConfirmedPublished = result.State == "Live";
                publication.LastConfirmedAt = now;
            }
            else if (result.State == "Submitted" && registry.Get(connection.ProviderKey).Descriptor.SupportsStatusChecks && command.Attempts < 20)
            {
                command.Operation = "CheckStatus";
                command.State = "Pending";
                command.AvailableAt = now.AddMinutes(1);
            }
            else if (result.RetrySafe && command.Attempts < 5)
            {
                command.State = "Pending";
                command.AvailableAt = now.AddSeconds(Math.Pow(2, command.Attempts) * 15);
                publication.State = "RetryPending";
            }
            else if (result.State != "Live" && result.State != "Withdrawn" && result.State != "NotPublished")
            {
                command.State = "NeedsAttention";
                publication.State = "NeedsAttention";
                publication.Error ??= "The provider has not confirmed the listing status. Review it before retrying.";
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
