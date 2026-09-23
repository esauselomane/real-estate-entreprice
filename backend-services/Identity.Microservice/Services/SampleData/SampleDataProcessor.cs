using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Access.SampleData;
using Shared.Presentation.Email;

namespace Identity.Microservice.Services.SampleData;

/// <summary>Resumes a durable provisioning job; service receipts make partial delivery safe to retry.</summary>
public sealed class SampleDataProcessor(IdentityDbContext database, IHttpClientFactory clients,
    TokenService tokens, IConfiguration configuration, IHostEnvironment environment,
    IOptions<EmailSettings> emailSettings, IEmailDelivery delivery)
{
    public async Task ProcessAsync(SampleDataJob job, CancellationToken cancellationToken)
    {
        var plan = JsonSerializer.Deserialize<SampleDataPlan>(job.PlanJson)
            ?? throw new InvalidOperationException("The sample data plan is missing.");
        if (job.ProvisionedAt == null)
        {
            await CreateBrokerAccountsAsync(plan, cancellationToken);
            using var client = clients.CreateClient("SampleData");
            var serviceAccount = new UserAccount { UserId = "sample-data-worker", FullName = "Sample data provisioning",
                TenantId = plan.TenantId, SystemRole = SampleDataBlueprint.ServiceRole };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                tokens.GenerateAccessToken(serviceAccount, new UserAccessClaim(), TimeSpan.FromMinutes(5)));
            using var propertyResponse = await client.PostAsJsonAsync(ServiceUrl("Property", 5002), plan, cancellationToken);
            propertyResponse.EnsureSuccessStatusCode();
            var properties = await propertyResponse.Content.ReadFromJsonAsync<SampleDataResult>(cancellationToken)
                ?? throw new InvalidOperationException("Property service did not return the sample property references.");
            using var crmResponse = await client.PostAsJsonAsync(ServiceUrl("Crm", 5003), new CrmSampleDataRequest(plan, properties), cancellationToken);
            crmResponse.EnsureSuccessStatusCode();
            job.ProvisionedAt = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }
        if (job.NotifiedAt == null)
        {
            var appUrl = configuration["Urls:FrontendClient"];
            if (!Uri.TryCreate(appUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                throw new InvalidOperationException("The application URL is not configured.");
            var content = new EmailContent("Biscove Real Estate Tech", "Your agency’s sample workspace is ready", "Hello,",
                $"<p>Your requested sample data for <strong>{WebUtility.HtmlEncode(plan.AgencyName)}</strong> is ready.</p>" +
                "<p>You can now explore five contacts, five brokers, five properties near your agency’s address, " +
                "one company, one legal entity, five leads and five linked deals.</p>" +
                "<p>Follow a contact through a lead and deal to see how your team can work together. " +
                "All sample people, properties and transactions are fictional and labelled for demonstration. " +
                "Sample brokers cannot sign in and have not been invited.</p><p>Sign in with your own account to get started.</p>",
                "Explore your agency", uri.AbsoluteUri, UseSystemLogo: true);
            await delivery.SendAsync(emailSettings.Value.FromAddress, plan.AgencyEmail, content,
                emailSettings.Value.SupportEmail, cancellationToken);
            job.NotifiedAt = DateTime.UtcNow;
        }
        job.State = "Completed";
        job.Error = null;
        await database.SaveChangesAsync(cancellationToken);
    }

    private string ServiceUrl(string service, int developmentPort)
    {
        var baseUrl = configuration[$"SampleData:{service}ServiceUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) && environment.IsDevelopment()) baseUrl = $"http://localhost:{developmentPort}";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException($"SampleData:{service}ServiceUrl is not configured.");
        return uri.AbsoluteUri.TrimEnd('/') + "/api/internal/sample-data";
    }

    private async Task CreateBrokerAccountsAsync(SampleDataPlan plan, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        for (var index = 0; index < 5; index++)
        {
            var brokerId = SampleDataBlueprint.Id(plan.TenantId, "broker", index);
            if (await database.UserAccounts.AnyAsync(item => item.UserId == brokerId, cancellationToken)) continue;
            database.UserAccounts.Add(new UserAccount { UserId = brokerId, TenantId = plan.TenantId,
                FullName = SampleDataBlueprint.BrokerName(index), SystemRole = "Broker", AccountStatus = "De-registered",
                Email = $"broker{index + 1}.{plan.TenantId.ToLowerInvariant()}@example.invalid", CreatedAt = plan.CreatedAt });
            database.UserAccessClaims.Add(new UserAccessClaim { UserId = brokerId, CanExportFinancialReports = false });
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
