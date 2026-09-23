using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.DTOs.Syndication;
using Property.Microservice.Models;
using Property.Microservice.Models.Syndication;

namespace Property.Microservice.Services.Syndication;

/// <summary>Atomically records desired publication state and durable work. Provider confirmation is stored separately.</summary>
public sealed class SyndicationCoordinator(PropertyDbContext database, ListingProviderRegistry registry)
{
    public async Task<SyndicationPublication> QueueAsync(PropertyAsset property, SyndicationConnection connection, bool publish, CancellationToken cancellationToken, bool reconcile = false)
    {
        if (property.TenantId != connection.TenantId) throw new InvalidOperationException("The portal connection belongs to another agency.");
        var provider = registry.Get(connection.ProviderKey);
        if (!provider.Descriptor.Ready) throw new ArgumentException(provider.Descriptor.SetupMessage);
        if ((!connection.Enabled || !connection.HasApiKey) && publish && !reconcile) throw new ArgumentException("Enable this agency's portal connection first.");
        if (!publish && !provider.Descriptor.SupportsWithdrawal) throw new ArgumentException("This provider does not support withdrawal.");
        var snapshot = CreateSnapshot(property);
        if (publish && !reconcile)
        {
            var errors = await provider.ValidateAsync(snapshot, connection, cancellationToken);
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        }
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await AcquireLockAsync(database, cancellationToken);
        var publication = await database.SyndicationPublications.SingleOrDefaultAsync(item => item.PropertyId == property.PropertyId && item.ConnectionId == connection.Id, cancellationToken);
        if (reconcile && publication == null) throw new ArgumentException("There is no publication to reconcile.");
        if (publish && !reconcile && publication?.State is "Submitted" or "NeedsAttention")
            throw new ArgumentException("Check the provider status before submitting another publication request.");
        if (publication == null)
        {
            publication = new SyndicationPublication { PropertyId = property.PropertyId, ConnectionId = connection.Id, TenantId = property.TenantId };
            database.SyndicationPublications.Add(publication);
        }
        publication.Revision++;
        if (!reconcile) publication.DesiredPublished = publish;
        publication.State = reconcile ? "Submitted" : publish ? "Queued" : "WithdrawalPending";
        publication.Error = null;
        publication.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        database.SyndicationCommands.Add(new SyndicationCommand
        {
            PublicationId = publication.Id, Revision = publication.Revision,
            Operation = reconcile ? "CheckStatus" : publish ? "Publish" : "Withdraw", SnapshotJson = JsonSerializer.Serialize(snapshot)
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return publication;
    }

    internal static ListingSnapshot CreateSnapshot(PropertyAsset property)
    {
        string[] photos;
        try { photos = JsonSerializer.Deserialize<string[]>(property.GalleryImageJson) ?? []; }
        catch (JsonException) { throw new ArgumentException("The property photo list is invalid."); }
        return new ListingSnapshot(property.PropertyId, property.TenantId, property.CompleteAddress,
            property.PropertyDescription, property.IsForSale, property.IsForRent, property.SalePrice,
            property.Latitude, property.Longitude, photos);
    }

    internal static Task AcquireLockAsync(PropertyDbContext database, CancellationToken cancellationToken)
    {
        if (!database.Database.IsSqlServer()) return Task.CompletedTask;
        return database.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource='listing-syndication', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; IF @result < 0 THROW 50001, 'Publishing is busy. Please retry.', 1;", cancellationToken);
    }
}
