using GownApi.Model;
using System.Xml.Linq;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace GownApi.Services
{
    public class PaystationPayMeService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PaystationPayMeService> _logger;

        public PaystationPayMeService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<PaystationPayMeService> logger)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> CreatePayMeUrlAsync(Orders order, CancellationToken ct = default)
        {
            
            var paystationId = _config["Paystation:PaystationId"];
            var gatewayId = _config["Paystation:GatewayId"];
            var paymeUrl = _config["Paystation:paymeUrl"];
            var hmacKey = _config["Paystation:HmacKey"];


            if (string.IsNullOrWhiteSpace(paystationId))
                throw new InvalidOperationException("Paystation:PaystationId is not configured.");

            if (string.IsNullOrWhiteSpace(gatewayId))
                throw new InvalidOperationException("Paystation:GatewayId is not configured.");

            if (string.IsNullOrWhiteSpace(paymeUrl))
                throw new InvalidOperationException("Paystation:PayMePurchaseUrl is not configured.");

            if (string.IsNullOrWhiteSpace(hmacKey))
                throw new InvalidOperationException("Paystation:HmacKey is not configured.");

            var client = _httpClientFactory.CreateClient();

            var orderName = $"{order.FirstName} {order.LastName} Ref-{order.Id}".Trim();

            //var amountInCents = Convert.ToInt32(order.OrderAmount);

            var amountInCents = Convert.ToInt32(Math.Round(order.OrderAmount * 100m, MidpointRounding.AwayFromZero));

            using var request = new HttpRequestMessage(HttpMethod.Post, paymeUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["paystation"] = "_empty",
                    ["pstn_nr"] = "T",
                    ["pstn_pi"] = paystationId,
                    ["pstn_gi"] = gatewayId,
                    ["pstn_co"] = "T",
                    //["pstn_am"] = order.OrderAmount.ToString(),
                    ["pstn_am"] = amountInCents.ToString(CultureInfo.InvariantCulture),
                    ["pstn_cu"] = "NZD",
                    ["pstn_tm"] = "T",
                    ["pstn_mr"] = order.Id.ToString(),
                    ["pstn_mo"] = orderName,
                    ["pstn_af"] = "cents",
                    ["pstn_tc"] = "T"
                })
            };

            var bodyString = await request.Content.ReadAsStringAsync(ct);

            var unixTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var stringToHash = unixTs + "paystation" + bodyString;

            var hmacHex = HmacSha512Hex(hmacKey, stringToHash);

            var requestUrl =
                paymeUrl
                + $"?pstn_HMACTimestamp={WebUtility.UrlEncode(unixTs)}"
                + $"&pstn_HMAC={WebUtility.UrlEncode(hmacHex)}";

            request.RequestUri = new Uri(requestUrl);

            _logger.LogInformation("PayMe request body: {Body}", bodyString);
            _logger.LogInformation(
                "PayMe request amount: {OrderAmountDollars} dollars, {AmountInCents} cents, OrderId: {OrderId}",
                order.OrderAmount,
                amountInCents,
                order.Id
                );

            using var response = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Failed to create PayMe URL. Status={(int)response.StatusCode}, Body={responseBody}");
            }

            var xml = XDocument.Parse(responseBody);

            var payMeOrderUrl = xml.Descendants("PayMeOrderURL").FirstOrDefault()?.Value;
            var ec = xml.Descendants("ec").FirstOrDefault()?.Value;
            var em = xml.Descendants("em").FirstOrDefault()?.Value;

            if (!string.IsNullOrWhiteSpace(payMeOrderUrl))
            {
                return payMeOrderUrl;
            }

            throw new InvalidOperationException(
                $"Paystation PayMe response did not return PaymeUrl. ec={ec}, em={em}, body={responseBody}");
        }

        private static string HmacSha512Hex(string key, string data)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}