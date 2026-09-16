using System.Text.Json;
using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using Identity.Microservice.DTOs;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class EditContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DealUpdateAcceptsStageNamesFromTheEditor()
    {
        var request = JsonSerializer.Deserialize<UpdateDealPayload>("""{"dealStage":"DiscoveryNeedsAnalysis","transactionValue":1200000}""", Options);
        Assert.Equal(DealWorkflowStage.DiscoveryNeedsAnalysis, request!.DealStage);
        Assert.Equal(1200000m, request.TransactionValue);
    }

    [Fact]
    public void ContactUpdateAcceptsMultipleTypesWithoutTenantOrNavigationData()
    {
        var request = JsonSerializer.Deserialize<UpdateContactRequest>("""{"fullName":"Test contact","contactType":"Buyer,Seller"}""", Options);
        Assert.Equal(ContactType.Buyer | ContactType.Seller, request!.ContactType);
        Assert.Null(typeof(UpdateContactRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void SelfProfileContractCannotChangeEmailRoleOrTenant()
    {
        var request = JsonSerializer.Deserialize<UpdateProfileRequest>("""{"fullName":"New name","email":"other@example.invalid","systemRole":"PlatformAdmin","tenantId":"other"}""", Options);
        Assert.Equal("New name", request!.FullName);
        Assert.Null(typeof(UpdateProfileRequest).GetProperty("Email"));
        Assert.Null(typeof(UpdateProfileRequest).GetProperty("SystemRole"));
        Assert.Null(typeof(UpdateProfileRequest).GetProperty("TenantId"));
    }
}
