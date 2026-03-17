using System.Globalization;
using System.Xml.Linq;

namespace GownApi.Services.Paystation
{
    public record PaystationQuickLookupResult(
        string? LookupCode,
        string? LookupMessage,
        string? PaystationTransactionId,
        string? MerchantSession,
        string? TransactionProcess,
        int? PurchaseAmountCents,
        int? TotalSuccessfulRefunds,
        string? PaystationErrorCode,
        string? PaystationErrorMessage,
        string? PaystationErrorMessageExtended
    );

    public static class PaystationQuickLookupParser
    {
        public static PaystationQuickLookupResult Parse(string xml)
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root; // <PaystationQuickLookup>

            string? Get(string path)
            {
                // path like "LookupStatus/LookupCode"
                var cur = root;
                foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
                    cur = cur?.Element(part);
                return cur?.Value?.Trim();
            }

            int? GetInt(string path)
            {
                var s = Get(path);
                if (string.IsNullOrWhiteSpace(s)) return null;
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    return v;
                return null;
            }

            return new PaystationQuickLookupResult(
                LookupCode: Get("LookupStatus/LookupCode"),
                LookupMessage: Get("LookupStatus/LookupMessage"),
                PaystationTransactionId: Get("LookupResponse/PaystationTransactionID"),
                MerchantSession: Get("LookupResponse/MerchantSession"),
                TransactionProcess: Get("LookupResponse/TransactionProcess"),
                PurchaseAmountCents: GetInt("LookupResponse/PurchaseAmount"),
                TotalSuccessfulRefunds: GetInt("LookupResponse/TotalSuccessfulRefunds"),
                PaystationErrorCode: Get("LookupResponse/PaystationErrorCode"),
                PaystationErrorMessage: Get("LookupResponse/PaystationErrorMessage"),
                PaystationErrorMessageExtended: Get("LookupResponse/PaystationErrorMessageExtended")
            );
        }
    }
}
