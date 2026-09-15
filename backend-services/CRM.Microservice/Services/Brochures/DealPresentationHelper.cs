using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Microservice.Services.Brochures;

/// <summary>Resolves brochure contacts only from the deal's assigned brokers within its tenant.</summary>
public static class DealPresentationHelper
{
    public static string[] BrokerIds(string? allocation) => (allocation ?? string.Empty)
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static async Task<DealPresentationDto> ResolveAsync(DealRecord deal, IQueryable<BrokerRecord> brokers, IQueryable<AgencyRecord> agencies)
    {
        var ids = BrokerIds(deal.AllocatedBrokerIds);
        var records = await brokers.AsNoTracking().Where(broker => broker.TenantId == deal.TenantId && ids.Contains(broker.BrokerId)).ToListAsync();
        var contacts = new List<DealBrokerContactDto>();
        foreach (var id in ids)
        {
            var broker = records.FirstOrDefault(broker => broker.BrokerId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (broker != null) contacts.Add(new(broker.BrokerId, broker.FullName, broker.Email, broker.Phone));
        }
        var agency = await agencies.AsNoTracking().FirstOrDefaultAsync(agency => agency.TenantId == deal.TenantId);
        return new DealPresentationDto(deal.TenantId, agency?.Name ?? "Agency", agency?.CompanyLogoUrl, contacts);
    }
}
