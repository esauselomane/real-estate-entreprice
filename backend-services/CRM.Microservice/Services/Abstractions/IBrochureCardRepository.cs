using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="BrochureCard"/> aggregate root.
/// </summary>
public interface IBrochureCardRepository : IRepository<BrochureCard>, ITenantScoped<BrochureCard>
{
}
