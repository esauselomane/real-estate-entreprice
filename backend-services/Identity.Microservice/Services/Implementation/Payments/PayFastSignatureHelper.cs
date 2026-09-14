using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>PayFast checkout/ITN signatures preserve field order; API signatures sort fields separately.</summary>
public static class PayFastSignatureHelper
{
    public static string Encode(string value) => System.Net.WebUtility.UrlEncode(value.Trim());

    public static Dictionary<string, string> Parse(string payload)
    {
        var result = new Dictionary<string, string>();
        foreach (var field in payload.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = field.Split('=', 2);
            var key = HttpUtility.UrlDecode(parts[0]);
            if (!result.TryAdd(key, parts.Length == 2 ? HttpUtility.UrlDecode(parts[1]) : string.Empty))
                throw new ArgumentException("Duplicate webhook field.");
        }
        return result;
    }

    public static string Sign(IEnumerable<KeyValuePair<string, string>> fields, string passphrase, bool includeEmptyFields = false)
    {
        var parts = new List<string>();
        foreach (var field in fields)
        {
            if (field.Key != "signature" && (includeEmptyFields || !string.IsNullOrWhiteSpace(field.Value)))
                parts.Add($"{field.Key}={Encode(field.Value)}");
        }
        if (!string.IsNullOrWhiteSpace(passphrase))
            parts.Add($"passphrase={Encode(passphrase)}");
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(string.Join("&", parts)))).ToLowerInvariant();
    }
}
