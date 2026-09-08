using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="LeadRecord"/> aggregate root.
/// </summary>
public interface ILeadRepository : IRepository<LeadRecord>, ITenantScoped<LeadRecord>, IStageable<LeadRecord>, IBrokerScoped<LeadRecord>
{
}
