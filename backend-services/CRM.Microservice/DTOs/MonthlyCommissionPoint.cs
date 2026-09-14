namespace CRM.Microservice.DTOs;

public sealed record MonthlyCommissionPoint(int Year, int Month, decimal Gci, int ClosedCount)
{
    public decimal Commission => Gci;
}
