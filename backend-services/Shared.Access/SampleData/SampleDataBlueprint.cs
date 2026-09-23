using System.Security.Cryptography;
using System.Text;

namespace Shared.Access.SampleData;

/// <summary>Stable identifiers and the shared five-record scenario keep cross-service links consistent.</summary>
public static class SampleDataBlueprint
{
    public const string ServiceRole = "SampleDataProvisioner";
    public static string Id(string tenantId, string kind, int index = 0) => $"DEMO-{tenantId}-{kind}-{index}";
    public static string BrokerName(int index) => new[] { "Alex Morgan", "Lebo Nkosi", "Sam Patel", "Jamie Adams", "Thandi Mokoena" }[index] + " (Sample)";
    public static string ContactName(int index) => new[] { "Taylor Jacobs", "Jordan Naidoo", "Casey Dlamini", "Riley Daniels", "Robin Ndlovu" }[index] + " (Sample)";
    // Stable pseudorandom choices keep CRM interests aligned with properties across retries.
    public static string PropertyTypeName(string tenantId, int index) =>
        new[] { "Commercial", "Residential", "Land" }[Choice(tenantId, index, "type", 3)];
    public static int UnitCount(string tenantId, int index) => 2 + Choice(tenantId, index, "units", 4);

    private static int Choice(string tenantId, int index, string purpose, int count)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId}:{index}:{purpose}"));
        return hash[0] % count;
    }

    public static decimal Price(int index) => 1500000m + index * 350000m;
    public static string PropertyAddress(string address, int index) => $"Sample property {index + 1} — vicinity of {address}";
    public static bool IsValid(SampleDataPlan plan) => !string.IsNullOrWhiteSpace(plan.TenantId) && plan.TenantId.Length <= 64 &&
        !string.IsNullOrWhiteSpace(plan.Address) && plan.Address.Length <= 1000 &&
        (plan.Latitude != 0 || plan.Longitude != 0) && plan.Latitude is >= -85 and <= 85 && plan.Longitude is >= -179 and <= 179;
}
