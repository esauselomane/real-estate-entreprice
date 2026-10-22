using System.Net;
using System.Net.Http.Json;
using Shared.Access.PublicEnquiries;
using Xunit;

namespace Developer.PublicListings.Tests;

internal sealed class EnquiryHttpClientFactory(EnquiryHttpHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

internal sealed class EnquiryHttpHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public WebsiteEnquiry? Enquiry { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        Enquiry = await request.Content!.ReadFromJsonAsync<WebsiteEnquiry>(cancellationToken);
        Assert.Equal("/api/crm/website-enquiries", request.RequestUri!.AbsolutePath);
        Assert.True(WebsiteEnquirySignature.Verify(Enquiry!, request.Headers.GetValues("X-Enquiry-Time").Single(), request.Headers.GetValues("X-Enquiry-Signature").Single(), "test-only-secret"));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { leadId = 91 }) };
    }
}
