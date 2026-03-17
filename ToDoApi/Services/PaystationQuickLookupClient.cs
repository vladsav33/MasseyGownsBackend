using GownApi.Model;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace GownApi.Services.Paystation
{
    /// <summary>
    /// Paystation Quick Lookup client (POST + HMAC).
    /// Returns raw XML response as string.
    /// Typed HttpClient version: DI provides HttpClient directly.
    /// </summary>
    public class PaystationQuickLookupClient
    {
        private readonly HttpClient _httpClient;
        private readonly PaystationOptions _options;

        public PaystationQuickLookupClient(
            HttpClient httpClient,
            IOptions<PaystationOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        /// <summary>
        /// Lookup by Paystation transaction id (ti).
        /// </summary>
        public async Task<string> LookupRawXmlByTxnIdAsync(string txnId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(txnId))
                throw new ArgumentException("txnId is required.", nameof(txnId));

            var pi = _options.PaystationId;
            var hmacKey = _options.HmacKey;
       

            var pairs = new List<KeyValuePair<string, string>>
            {
                new("pi", pi),
                new("ti", txnId.Trim())
            };

            return await PostWithHmacReturnXmlAsync(pairs, hmacKey, ct);
        }

        /// <summary>
        /// Lookup by merchant session (ms).
        /// </summary>
        public async Task<string> LookupRawXmlByMerchantSessionAsync(string merchantSession, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(merchantSession))
                throw new ArgumentException("merchantSession is required.", nameof(merchantSession));

            var pi = _options.PaystationId;
            var hmacKey = _options.HmacKey;

            var pairs = new List<KeyValuePair<string, string>>
            {
                new("pi", pi),
                new("ms", merchantSession.Trim())
            };

            return await PostWithHmacReturnXmlAsync(pairs, hmacKey, ct);
        }

        /// <summary>
        /// Lookup by both merchant session (ms) and transaction id (ti) if you want.
        /// </summary>
        public async Task<string> LookupRawXmlAsync(string? txnId, string? merchantSession, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(txnId) && string.IsNullOrWhiteSpace(merchantSession))
                throw new ArgumentException("Either txnId or merchantSession must be provided.");

            var pi = _options.PaystationId;
            var hmacKey = _options.HmacKey;

            var pairs = new List<KeyValuePair<string, string>> { new("pi", pi) };

            if (!string.IsNullOrWhiteSpace(merchantSession))
                pairs.Add(new("ms", merchantSession.Trim()));

            if (!string.IsNullOrWhiteSpace(txnId))
                pairs.Add(new("ti", txnId.Trim()));

            return await PostWithHmacReturnXmlAsync(pairs, hmacKey, ct);
        }

        // =========================
        // Core HMAC POST
        // =========================
        private async Task<string> PostWithHmacReturnXmlAsync(
            List<KeyValuePair<string, string>> pairs,
            string hmacKey,
            CancellationToken ct)
        {
            using var content = new FormUrlEncodedContent(pairs);
            var bodyString = await content.ReadAsStringAsync(ct);

            var unixTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var stringToHash = unixTs + "paystation" + bodyString;

            var hmacHex = HmacSha512Hex(hmacKey, stringToHash);
            var LookupUrl = _options.LookupUrl;

            var url =
                LookupUrl
                + $"?pstn_HMACTimestamp={WebUtility.UrlEncode(unixTs)}"
                + $"&pstn_HMAC={WebUtility.UrlEncode(hmacHex)}";

            using var resp = await _httpClient.PostAsync(url, content, ct);
            var xml = await resp.Content.ReadAsStringAsync(ct);

            // return raw XML even on "access denied"
            return xml;
        }


        private static string HmacSha512Hex(string key, string data)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
