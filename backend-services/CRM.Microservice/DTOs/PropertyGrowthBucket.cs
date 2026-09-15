namespace CRM.Microservice.DTOs;

/// <summary>Calendar bucket returned by the Property service's growth endpoint.</summary>
public sealed record PropertyGrowthBucket(int Year, int Month, int PropertyCount);
