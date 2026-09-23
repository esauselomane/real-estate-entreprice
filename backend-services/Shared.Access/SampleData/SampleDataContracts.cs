namespace Shared.Access.SampleData;

/// <summary>Tenant-bound, versioned onboarding data passed only between backend services.</summary>
public sealed record SampleDataPlan(string TenantId, string AgencyName, string AgencyEmail, string Address,
    decimal Latitude, decimal Longitude, DateTime CreatedAt, bool IsTrial = true, DateTime? TrialStartDate = null, DateTime? TrialEndDate = null);
public sealed record SamplePropertyReference(int Index, int PropertyId, string Address, decimal Price, string PropertyTypeName = "Residential");
public sealed record SampleDataResult(IReadOnlyList<SamplePropertyReference> Properties);

/// <summary>Marks internal provisioning endpoints exempt from user subscription checks.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SampleDataEndpointAttribute : Attribute;
public sealed record CrmSampleDataRequest(SampleDataPlan Plan, SampleDataResult PropertyResult);
