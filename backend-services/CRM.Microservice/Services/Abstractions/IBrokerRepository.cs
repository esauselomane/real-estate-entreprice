using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="BrokerRecord"/> aggregate root.
/// </summary>
public interface IBrokerRepository : IRepository<BrokerRecord>, ITenantScoped<BrokerRecord>, IAgencyScoped<BrokerRecord>
{
}
