using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Brochures;
using CRM.Microservice.Services.Reporting;
using Shared.Presentation.Email;
using Xunit;

namespace Identity.Payments.Tests;

public class PresentationAndReportingTests
{
    [Fact]
    public void RealisedCommissionPreservesExplicitZeroAndIgnoresOpenDeals()
    {
        var deal = CreateDeal();
        Assert.Equal(5000m, CommissionCalculator.Expected(deal));
        Assert.Equal(0m, CommissionCalculator.Realised(deal));
        deal.DealStage = DealWorkflowStage.ClosedWon;
        Assert.Equal(5000m, CommissionCalculator.Realised(deal));
        deal.RealisedCommission = 0m;
        Assert.Equal(0m, CommissionCalculator.Realised(deal));
        deal.DealStage = DealWorkflowStage.ClosedLost;
        Assert.Equal(0m, CommissionCalculator.Pipeline(deal));
    }

    [Fact]
    public void RevenueUsesCloseDateAndReconcilesToMonthlyTotals()
    {
        var deal = CreateDeal();
        deal.CreatedAt = new DateTime(2025, 12, 1);
        deal.LastStageUpdatedAt = new DateTime(2026, 2, 1);
        deal.DealStage = DealWorkflowStage.ClosedWon;
        var report = ReportSummaryHelper.Build(2026, [], [deal]);
        Assert.Equal(0, report.TotalDeals);
        Assert.Equal(5000m, report.TotalRevenue);
        Assert.Equal(report.TotalRevenue, report.MonthlyBreakdown.Sum(row => row.Revenue));
        var trend = CommissionTrendHelper.Build([deal], new DateTime(2026, 1, 1), 3);
        Assert.Equal(3, trend.Count);
        Assert.Equal(0m, trend[0].Gci);
        Assert.Equal(5000m, trend[1].Gci);
        Assert.Equal(1, trend[1].ClosedCount);
        Assert.Equal(0m, trend[2].Gci);
    }

    [Fact]
    public void FlatCommissionIsNotMultipliedByTransactionValue()
    {
        var deal = CreateDeal();
        deal.CommissionType = CommissionTypes.Amount;
        deal.CommissionValue = 750m;
        Assert.Equal(750m, CommissionCalculator.Expected(deal));
    }

    [Fact]
    public void BrochureIncludesBothBrokersAndEncodesTheirDetails()
    {
        var html = BrokerCoverHelper.Render([
            new DealBrokerContactDto("1", "Alex & Co", "alex@example.com", "123"),
            new DealBrokerContactDto("2", "<script>bad</script>", "other@example.com", "456")]);
        Assert.Contains("Alex &amp; Co", html);
        Assert.Contains("other@example.com", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("flex-wrap:wrap", html);
    }

    [Fact]
    public void SystemEmailEmbedsLogoAndAgencyEmailUsesAgencyBrand()
    {
        using var message = EmailLayout.CreateMessage("sender@example.com", "recipient@example.com",
            new EmailContent("System", "Welcome", "Hello", "<p>Welcome</p>", UseSystemLogo: true));
        Assert.Contains(message.AlternateViews, view => view.LinkedResources.Count == 1);
        var html = EmailLayout.Render(new EmailContent("Agency & Co", "Reminder", "Hello <User>", "<p>Reminder</p>", LogoUrl: "https://agency.example/logo.png"));
        Assert.Contains("Agency &amp; Co", html);
        Assert.Contains("https://agency.example/logo.png", html);
        Assert.DoesNotContain("Hello <User>", html);
        Assert.Null(EmailLayout.SafeUrl("javascript:alert(1)"));
    }

    [Fact]
    public void PackageManagementRequiresExactSuperAdminRole()
    {
        var policy = Attribute.GetCustomAttribute(typeof(Identity.Microservice.Controllers.PricingPackagesController),
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute)) as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
        Assert.Equal("SuperAdmin", policy?.Roles);
    }

    [Fact]
    public void PackageValidationRejectsNegativePricesAndNullFeatures()
    {
        var request = new Identity.Microservice.DTOs.PricingPackageAdminRequest
        {
            Code = "SME", Name = "Agency", MonthlyPrice = -1m, Features = null!
        };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request,
            new System.ComponentModel.DataAnnotations.ValidationContext(request), results, true));
        Assert.Contains(results, result => result.MemberNames.Contains("MonthlyPrice"));
        Assert.Contains(results, result => result.MemberNames.Contains("Features"));
    }

    [Fact]
    public void BrokerIdentifiersAreExactAndDeduplicated()
    {
        Assert.Equal(new[] { "BROKER-1", "BROKER-10" }, DealPresentationHelper.BrokerIds(" BROKER-1, BROKER-10,broker-1,, "));
        var deal = CreateDeal();
        deal.AllocatedBrokerIds = "BROKER-10";
        deal.DealStage = DealWorkflowStage.ClosedWon;
        var row = BrokerReportHelper.BuildPerformanceRow(new BrokerRecord { BrokerId = "BROKER-1" }, [deal]);
        Assert.Equal(0m, row.TotalGci);
        Assert.Equal(0, row.DealsClosed);
        Assert.Null(row.ActiveListings);
    }

    private static DealRecord CreateDeal() => new()
    {
        TransactionValue = 100000m, CommissionType = CommissionTypes.Percent,
        CommissionValue = 5m, DealStage = DealWorkflowStage.DiscoveryNeedsAnalysis
    };
}
