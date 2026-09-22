using System.Net;
using System.Text;

namespace Identity.Payments.Tests;

internal sealed class ResendTestHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    public HttpStatusCode StatusCode { get; set; } = statusCode;
    public string? Body { get; private set; }
    public string? RequestPath { get; private set; }
    public string? Authorization { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Body = await request.Content!.ReadAsStringAsync(cancellationToken);
        RequestPath = request.RequestUri!.AbsolutePath;
        Authorization = request.Headers.Authorization?.ToString();
        var body = StatusCode == HttpStatusCode.OK
            ? "{\"id\":\"11111111-1111-1111-1111-111111111111\"}"
            : "{\"name\":\"validation_error\",\"message\":\"Sender domain is not verified\",\"statusCode\":403}";
        return new HttpResponseMessage(StatusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
