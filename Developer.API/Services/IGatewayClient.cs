using System.Net.Http;
using System.Threading.Tasks;

namespace Developer.API.Services
{
    public interface IGatewayClient
    {
        Task<HttpResponseMessage> GetAsync(string path, string? agencyId = null, string? serviceToken = null);
        Task<HttpResponseMessage> PostAsync(string path, object? body, string? agencyId = null, string? serviceToken = null);
        Task<HttpResponseMessage> PutAsync(string path, object? body, string? agencyId = null, string? serviceToken = null);
        Task<HttpResponseMessage> DeleteAsync(string path, string? agencyId = null, string? serviceToken = null);
    }
}