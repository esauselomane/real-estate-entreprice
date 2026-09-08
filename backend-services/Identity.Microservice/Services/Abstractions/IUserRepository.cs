using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="UserAccount"/> aggregate root.
/// </summary>
public interface IUserRepository : IRepository<UserAccount>, ITenantScoped<UserAccount>, IEmailLookup<UserAccount>
{
}
