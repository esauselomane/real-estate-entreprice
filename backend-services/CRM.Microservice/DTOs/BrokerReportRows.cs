namespace CRM.Microservice.DTOs;

public sealed record BrokerLeaderboardRow(string Agent, string AgentIds, int Deals, decimal TotalValue, decimal TotalCommission, int Won);
public sealed record BrokerTargetProgressRow(int Year, int Month, string Period, decimal Target, decimal Realised, decimal Progress);
public sealed record BrokerMonthlyTrendRow(string Period, decimal Gci, decimal? CompanyDollar, int ClosedCount);

public sealed record BrokerPerformanceRow(string BrokerId, string Name, int? ActiveListings, int DealsClosed, decimal TotalGci);
