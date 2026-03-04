using GownApi.Services.Paystation;
using Microsoft.AspNetCore.Builder;

namespace GownApi.Endpoints
{
    public static class AdminRefundLookupEndpoints
    {
        public static void MapAdminRefundLookupEndpoints(this WebApplication app)
        {
           
            app.MapGet("/api/admin/paystation/lookup/{txnId}", async (
                string txnId,
                PaystationQuickLookupClient client) =>
            {
                var xml = await client.LookupRawXmlByTxnIdAsync(txnId);

               
                var parsed = PaystationQuickLookupParser.Parse(xml);

                
                return Results.Ok(new
                {
                    rawXml = SafeSnippet(xml, 2000),
                    result = parsed
                });
            });
        }

        private static string SafeSnippet(string? text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= maxLen ? text : text[..maxLen] + "...";
        }
    }
}
