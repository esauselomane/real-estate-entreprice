using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="DealRecord"/> aggregate root.
/// </summary>
public interface IDealRepository : IRepository<DealRecord>, ITenantScoped<DealRecord>, IStageable<DealRecord>, IBrokerScoped<DealRecord>
{
}
