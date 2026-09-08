using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="CRMCompany"/> aggregate root.
/// </summary>
public interface ICompanyRepository : IRepository<CRMCompany>, ITenantScoped<CRMCompany>
{
}
