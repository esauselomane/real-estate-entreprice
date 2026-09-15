using System.Net;
using System.Text;
using CRM.Microservice.DTOs;

namespace CRM.Microservice.Services.Brochures;

public static class BrokerCoverHelper
{
    public static string Render(IReadOnlyList<DealBrokerContactDto> brokers)
    {
        if (brokers.Count == 0) return "<p>No broker assigned to this deal.</p>";
        var html = new StringBuilder("<div style=\"display:flex;flex-wrap:wrap;gap:20px\">");
        foreach (var broker in brokers)
        {
            html.Append("<section style=\"flex:1 1 40%;min-width:180px;break-inside:avoid\">");
            html.Append($"<h3 style=\"margin:0 0 8px;color:#0f172a\">{WebUtility.HtmlEncode(broker.FullName)}</h3>");
            html.Append($"<p style=\"font-size:12px;color:#475569\">{WebUtility.HtmlEncode(broker.Email)}<br>{WebUtility.HtmlEncode(broker.Phone)}</p></section>");
        }
        return html.Append("</div>").ToString();
    }
}
