using System.Text.Json;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Access.SampleData;

namespace CRM.Microservice.Services.SampleData;

/// <summary>Commits the linked CRM demonstration and its receipt atomically, once per agency.</summary>
public sealed class SampleDataSeeder(CRMDbContext database)
{
    public async Task SeedAsync(CrmSampleDataRequest request, CancellationToken cancellationToken)
    {
        var plan = request.Plan;
        Validate(request);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await SampleDataStorage.LockAsync(database, plan.TenantId, cancellationToken);
        if (await database.SampleDataReceipts.AnyAsync(item => item.TenantId == plan.TenantId, cancellationToken)) return;
        var agency = await database.AgencyRecords.SingleOrDefaultAsync(item => item.TenantId == plan.TenantId, cancellationToken);
        if (agency == null)
        {
            agency = new AgencyRecord { AgencyId = SampleDataBlueprint.Id(plan.TenantId, "agency"), TenantId = plan.TenantId,
                Name = plan.AgencyName, ContactEmail = plan.AgencyEmail, HeadquartersAddress = plan.Address,
                IsTrial = plan.IsTrial, TrialStartDate = plan.TrialStartDate, TrialEndDate = plan.TrialEndDate };
            database.AgencyRecords.Add(agency);
        }
        var company = new CRMCompany { CompanyId = SampleDataBlueprint.Id(plan.TenantId, "company"), TenantId = plan.TenantId,
            BusinessName = "Sample Neighbourhood Investments", HeadOfficeAddress = plan.Address, CorporateCreditLimit = 15000000m };
        database.CRMCompanies.Add(company);
        await database.SaveChangesAsync(cancellationToken);
        var legalEntity = new CRMLegalEntity { LegalEntityId = SampleDataBlueprint.Id(plan.TenantId, "legal"), TenantId = plan.TenantId,
            RegisteredLegalName = "Sample Neighbourhood Holdings (Pty) Ltd", AssociatedCompanyId = company.CompanyId };
        database.CRMLegalEntities.Add(legalEntity);
        await database.SaveChangesAsync(cancellationToken);
        company.AssociatedLegalEntityId = legalEntity.LegalEntityId;
        var contacts = new List<Contact>();
        var leads = new List<LeadRecord>();
        for (var index = 0; index < 5; index++)
        {
            var brokerId = SampleDataBlueprint.Id(plan.TenantId, "broker", index);
            var contact = new Contact { ContactId = SampleDataBlueprint.Id(plan.TenantId, "contact", index), TenantId = plan.TenantId,
                AgencyId = agency.AgencyId, FullName = SampleDataBlueprint.ContactName(index), ContactType = ContactType.Buyer | ContactType.Owner | ContactType.Seller,
                EmailAddress = $"contact{index + 1}.{plan.TenantId.ToLowerInvariant()}@example.invalid", CompanyName = company.BusinessName,
                AssociatedCompanyId = company.CompanyId, AssociatedLegalEntityId = legalEntity.LegalEntityId,
                PrimaryLocation = plan.Address, BudgetLimit = SampleDataBlueprint.Price((index + 4) % 5),
                PropertyTypeInterests = request.PropertyResult.Properties.Single(item => item.Index == (index + 4) % 5).PropertyTypeName, Notes = "SAMPLE — fictional investor; owns one demonstration home and is buying another." };
            contacts.Add(contact);
            database.Contacts.Add(contact);
            database.BrokerRecords.Add(new BrokerRecord { BrokerId = brokerId, TenantId = plan.TenantId, AgencyId = agency.AgencyId,
                FullName = SampleDataBlueprint.BrokerName(index), Email = $"broker{index + 1}.{plan.TenantId.ToLowerInvariant()}@example.invalid", CreatedAt = plan.CreatedAt });
            // Contact i+1 buys contact i's property, avoiding self-sales while connecting the entire scenario.
            var buyerIndex = (index + 1) % 5;
            var lead = new LeadRecord { TenantId = plan.TenantId, ClientName = SampleDataBlueprint.ContactName(buyerIndex),
                LinkedContactId = SampleDataBlueprint.Id(plan.TenantId, "contact", buyerIndex),
                ContactEmail = $"contact{buyerIndex + 1}.{plan.TenantId.ToLowerInvariant()}@example.invalid", AllocatedBrokerIds = brokerId,
                LeadStage = LeadWorkflowStage.Qualified, PropertyInterestClass = request.PropertyResult.Properties.Single(item => item.Index == index).PropertyTypeName, PreferredArea = plan.Address,
                EstimatedBudget = SampleDataBlueprint.Price(index), CommissionType = CommissionTypes.Percent, CommissionValue = 3,
                IsConvertedToDeal = true, LeadNotes = "SAMPLE — qualified enquiry promoted into its linked deal.", CreatedAt = plan.CreatedAt };
            leads.Add(lead);
            database.LeadRecords.Add(lead);
        }
        await database.SaveChangesAsync(cancellationToken);
        var deals = new List<DealRecord>();
        var stages = new[] { DealWorkflowStage.DiscoveryNeedsAnalysis, DealWorkflowStage.Presentation, DealWorkflowStage.ProposalQuoteSent, DealWorkflowStage.Negotiation, DealWorkflowStage.ClosedWon };
        for (var index = 0; index < 5; index++)
        {
            var property = request.PropertyResult.Properties.Single(item => item.Index == index);
            var deal = new DealRecord { TenantId = plan.TenantId, AllocatedBrokerIds = SampleDataBlueprint.Id(plan.TenantId, "broker", index),
                BuyerClientId = contacts[(index + 1) % 5].ContactId, SellerClientId = contacts[index].ContactId,
                LinkedPropertyId = property.PropertyId, PropertyAddress = property.Address, TransactionValue = property.Price,
                DealStage = stages[index], CommissionType = CommissionTypes.Percent, CommissionValue = 3,
                RealisedCommission = index == 4 ? property.Price * 0.03m : null, RealisedCommissionIsAuto = index == 4,
                SourceLeadId = leads[index].LeadId, DealNotes = "SAMPLE — demonstration transaction, not a real agreement.", CreatedAt = plan.CreatedAt };
            deal.BrokerSplits.Add(new DealBrokerSplit { BrokerId = deal.AllocatedBrokerIds, BrokerName = SampleDataBlueprint.BrokerName(index),
                SplitPercentage = 100, SplitAmount = property.Price * 0.03m, CreatedAt = plan.CreatedAt });
            deals.Add(deal);
            database.DealRecords.Add(deal);
        }
        await database.SaveChangesAsync(cancellationToken);
        for (var index = 0; index < 5; index++)
        {
            contacts[index].LinkedDealIds = $"{deals[index].DealId},{deals[(index + 4) % 5].DealId}";
            leads[index].LeadNotes += $" Deal reference: {deals[index].DealId}.";
        }
        database.SampleDataReceipts.Add(new SampleDataReceipt { TenantId = plan.TenantId, ResultJson = JsonSerializer.Serialize(request.PropertyResult) });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static void Validate(CrmSampleDataRequest request)
    {
        if (!SampleDataBlueprint.IsValid(request.Plan) || request.PropertyResult.Properties.Count != 5 ||
            request.PropertyResult.Properties.Select(item => item.Index).Distinct().Count() != 5 ||
            request.PropertyResult.Properties.Any(item => item.Index is < 0 or > 4 || item.PropertyId <= 0 || item.Price != SampleDataBlueprint.Price(item.Index)) ||
            request.PropertyResult.Properties.Select(item => item.PropertyId).Distinct().Count() != 5)
            throw new ArgumentException("Exactly five correctly mapped properties are required.");
    }
}
