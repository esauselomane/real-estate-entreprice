namespace CRM.Microservice.DTOs;

/// <summary>Agent codes indexed by provider key and environment, for example private-property:Sandbox.</summary>
public sealed record BrokerSyndicationCodesRequest(Dictionary<string, string> Codes);
