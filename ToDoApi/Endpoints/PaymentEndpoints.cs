//using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using System.Web;

namespace GownApi.Endpoints
{
    public static class PaymentEndpoints
    {
        // Config (in production → move to appsettings.json)
        const string PaystationInitiationUrl = "https://www.paystation.co.nz/direct/paystation.dll";
        const string MerchantId = "617970";
        const string GatewayId = "DEVELOPMENT";
        const string ReturnUrl = "https://yourdomain.com/checkout"; // React route

        record PaymentRequest(int Amount, string OrderId);

        public static void MapPaymentEndpoints(this WebApplication app)
        {
            app.MapPost("/api/payment/create-payment", async (
            PaymentRequest request,
            [FromServices] IHttpClientFactory httpClientFactory) =>
            {
                var httpClient = httpClientFactory.CreateClient();
                var values = new Dictionary<string, string>
                {
                    { "paystation", "_empty" },             // required
                    { "pstn_pi", MerchantId },              // Paystation account ID
                    { "pstn_gi", GatewayId },               // Gateway ID
                    { "pstn_am", request.Amount.ToString() }, // Amount in cents (e.g. 1000 = $10.00)
                    { "pstn_ms", Guid.NewGuid().ToString() }, // Merchant session ID
                    { "pstn_nr", "t" },                     // test/live flag
                    //{ "pstn_du", ReturnUrl }                // Return URL (optional depending on flow)
                };

                var content = new FormUrlEncodedContent(values);
                var response = await httpClient.PostAsync(PaystationInitiationUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                string redirectUrl = ExtractRedirectUrl(responseString);
                var rawHTML = await response.Content.ReadAsStringAsync();

                return Results.Ok(new
                {
                    redirectUrl
                    //rawHTML
                });
            });

            // Utility function for XML extraction
            static string ExtractRedirectUrl(string xml)
            {
                var start = xml.IndexOf("<DigitalOrder>");
                var end = xml.IndexOf("</DigitalOrder>");
                if (start >= 0 && end > start)
                {
                    return xml.Substring(start + 14, end - (start + 14));
                }
                return string.Empty;
            }
        }
    }
}
