using System.Net;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Retries read-only validation requests; payment creation is never blindly retried.</summary>
public static class PaymentHttpHelper
{
    public static async Task<string> PostValidationAsync(HttpClient client, string url, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var content = new FormUrlEncodedContent(fields.Where(field => field.Key != "signature"));
            using var response = await client.PostAsync(url, content, cancellationToken);
            if (attempt < 2 && (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt)), cancellationToken);
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
    }
}
