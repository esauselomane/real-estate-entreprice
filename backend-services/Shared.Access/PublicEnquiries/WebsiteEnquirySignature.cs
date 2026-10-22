using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Shared.Access.PublicEnquiries;

/// <summary>Authenticates the exact enquiry and a short-lived timestamp between website services.</summary>
public static class WebsiteEnquirySignature
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Create(WebsiteEnquiry enquiry, string timestamp, string secret)
    {
        var payload = "website-enquiry-v1\n" + timestamp + "\n" + JsonSerializer.Serialize(enquiry, SerializerOptions);
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
    }

    public static bool Verify(WebsiteEnquiry enquiry, string timestamp, string signature, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || !long.TryParse(timestamp, out var seconds)) return false;
        if (seconds < DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 120 || seconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30) return false;
        if (signature.Length != 64) return false;
        var expected = Encoding.UTF8.GetBytes(Create(enquiry, timestamp, secret));
        return CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(signature));
    }
}
