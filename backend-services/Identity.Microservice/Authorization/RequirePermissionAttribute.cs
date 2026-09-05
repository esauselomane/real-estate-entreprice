using Microsoft.AspNetCore.Authorization;
namespace Identity.Microservice.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        public RequirePermissionAttribute(string permission)
        {
            Permission = permission;
            Policy = $"Permission:{permission}";
        }
        public string Permission { get; }
    }
}
