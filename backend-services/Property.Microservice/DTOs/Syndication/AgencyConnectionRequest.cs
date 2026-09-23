namespace Property.Microservice.DTOs.Syndication;

public sealed record AgencyConnectionRequest(string Environment, string BranchId, string? ApiKey = null, bool RemoveApiKey = false, Dictionary<string, string>? Settings = null);
