using GownApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using GownApi;
using GownApi.Model;
using System.Globalization;

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
            // ---------------- create payment (initiate Paystation) ----------------
            app.MapPost("/api/payment/create-payment", async (
                PaymentRequest request,
                [FromServices] IHttpClientFactory httpClientFactory) =>
            {
                var httpClient = httpClientFactory.CreateClient();
                var values = new Dictionary<string, string>
                {
                    { "paystation", "_empty" },
                    { "pstn_pi", MerchantId },
                    { "pstn_gi", GatewayId },
                    { "pstn_am", request.Amount.ToString() },  // cents
                    { "pstn_ms", Guid.NewGuid().ToString() },  // merchant session
                    { "pstn_nr", "t" }                          // test flag
                    //{ "pstn_du", ReturnUrl }
                };

                var content = new FormUrlEncodedContent(values);
                var response = await httpClient.PostAsync(PaystationInitiationUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                string redirectUrl = ExtractRedirectUrl(responseString);

                return Results.Ok(new
                {
                    redirectUrl
                });
            });

            // ---------------- Paystation server-to-server notify ----------------
            app.MapPost("/notify", async (HttpRequest req, IConfiguration config, GownDb db) =>
            {
                // Read raw body so we can log and parse XML
                req.EnableBuffering();
                using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
                var raw = await reader.ReadToEndAsync();
                req.Body.Position = 0;

                Console.WriteLine("=== Paystation POST notify ===");
                Console.WriteLine($"Content-Type: {req.ContentType}");
                foreach (var h in req.Headers)
                {
                    Console.WriteLine($"H {h.Key}: {h.Value}");
                }
                Console.WriteLine("RAW BODY:");
                Console.WriteLine(raw);

                // Parse XML
                var doc = XDocument.Parse(raw);
                string? Get(string name) => doc.Root?.Element(name)?.Value;

                int ec = int.TryParse(Get("ec"), out var ecVal) ? ecVal : -1;
                var msg = Get("em");
                var txnId = Get("PaystationTransactionID")
                            ?? Get("TransactionID")
                            ?? Get("ti");
                var merchantSession = Get("MerchantSession");
                var txnTime = Get("TransactionTime") ?? Get("DigitalReceiptTime");
                var purchaseAmountCents = int.TryParse(Get("PurchaseAmount"), out var cents) ? cents : 0;
                var purchaseAmount = purchaseAmountCents / 100m; // Amount Paid from Paystation

                if (ec != 0)
                {
                    Console.WriteLine($"Paystation notify with ec={ec}, em={msg}, ignored.");
                    return Results.Ok("OK");
                }

                Console.WriteLine($"Paystation SUCCESS: txn={txnId}, amount={purchaseAmount}, ms={merchantSession}");

                // For now: use the most recent order
                var order = await db.orders
                    .OrderByDescending(o => o.Id)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    Console.WriteLine("No order found – skip sending email.");
                    return Results.Ok("NO_ORDER");
                }

                // Load ordered items for this order
                var orderedItems = await db.orderedItems
                    .Where(oi => oi.OrderId == order.Id)
                    .ToListAsync();

                // Join with Sku + Items to get item names
                var skuIds = orderedItems.Select(oi => oi.SkuId).Distinct().ToList();
                var skuList = await db.Sku
                    .Where(s => skuIds.Contains(s.Id))
                    .ToListAsync();

                var itemIds = skuList.Select(s => s.ItemId).Distinct().ToList();
                var items = await db.items
                    .Where(i => itemIds.Contains(i.Id))
                    .ToListAsync();

                // Calculate totals (Grand Total / Amount Paid / Balance Owing)
                decimal total = 0m;

                var sbRows = new StringBuilder();
                foreach (var oi in orderedItems)
                {
                    var sku = skuList.FirstOrDefault(s => s.Id == oi.SkuId);
                    var item = sku != null ? items.FirstOrDefault(i => i.Id == sku.ItemId) : null;

                    var itemName = item?.Name ?? $"Item {oi.SkuId}";
                    int qty = oi.Quantity;
                    decimal price = (decimal)oi.Cost;
                    decimal lineTotal = price * qty;
                    decimal gst = price * 0.15m; // GST per item = price * 15%

                    total += lineTotal;

                    sbRows.AppendLine($@"
<tr>
  <td>{WebUtility.HtmlEncode(itemName)}</td>
  <td>{qty}</td>
  <td>{price:0.00}</td>
  <td>{gst:0.00}</td>
  <td>{lineTotal:0.00}</td>
</tr>");
                }

                decimal amountPaid = purchaseAmount;           // From Paystation
                decimal balanceOwing = total - amountPaid;     // Grand Total - Amount Paid

                var cartRowsHtml = sbRows.ToString();

                // Load email template
                var template = await db.EmailTemplates
                    .AsNoTracking()
                    .SingleOrDefaultAsync(t => t.Name == "PaymentCompleted");

                string subject;
                string bodyHtml;
                string taxReceiptHtml;

                if (template != null)
                {
                    var invoiceDate = order.OrderDate
                    .ToDateTime(TimeOnly.MinValue)
                    .ToString("dd MMM yyyy", CultureInfo.CreateSpecificCulture("en-NZ"));

                    var values = new Dictionary<string, string?>
                    {
                        ["TransactionId"] = txnId ?? "",
                        ["GstNumber"] = "", // can come from config later
                        ["InvoiceNumber"] = order.Id.ToString(),
                        ["InvoiceDate"] = invoiceDate,
                        ["FirstName"] = order.FirstName,
                        ["LastName"] = order.LastName,
                        ["Address"] = order.Address,
                        ["City"] = order.City,
                        ["Postcode"] = order.Postcode,
                        ["Country"] = order.Country,
                        ["StudentId"] = order.StudentId.ToString(),
                        ["Email"] = order.Email,
                        ["Total"] = total.ToString("0.00"),
                        ["AmountPaid"] = amountPaid.ToString("0.00"),
                        ["BalanceOwing"] = balanceOwing.ToString("0.00"),
                        ["CartRows"] = cartRowsHtml,
                        ["Message"] = msg ?? "",
                        ["MerchantSession"] = merchantSession ?? "",
                        ["TransactionTime"] = txnTime ?? ""
                    };

                    subject = ApplyTemplate(template.SubjectTemplate, values);
                    bodyHtml = ApplyTemplate(template.BodyHtml, values);
                    taxReceiptHtml = ApplyTemplate(template.TaxReceiptHtml, values);

                    var fullHtml = $@"
<html>
  <body>
    {bodyHtml}
    <hr />
    {taxReceiptHtml}
  </body>
</html>";

                    bodyHtml = fullHtml;
                }
                else
                {
                    subject = "[TEST] Paystation payment successful (no template)";
                    bodyHtml = $@"
<p>A Paystation payment has succeeded (test environment).</p>
<ul>
  <li><b>Result</b>: {WebUtility.HtmlEncode(msg ?? "")}</li>
  <li><b>Transaction ID</b>: {WebUtility.HtmlEncode(txnId ?? "")}</li>
  <li><b>Amount (Paystation)</b>: {purchaseAmount:0.00}</li>
  <li><b>Grand Total</b>: {total:0.00}</li>
  <li><b>Balance Owing</b>: {balanceOwing:0.00}</li>
  <li><b>MerchantSession</b>: {WebUtility.HtmlEncode(merchantSession ?? "")}</li>
  <li><b>Time</b>: {WebUtility.HtmlEncode(txnTime ?? "")}</li>
</ul>";
                }

                // SMTP configuration (Mailtrap etc.)
                var smtpHost = config["Smtp:Host"];
                var smtpPort = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
                var smtpUser = config["Smtp:Username"];
                var smtpPass = config["Smtp:Password"];

                var fromAddress = config["Email:From"] ?? smtpUser;

                // For now still send to test inbox; later you can switch to order.Email
                var toAddress = config["Email:To"] ?? smtpUser;
                // var toAddress = order.Email;

                using var client = new SmtpClient(smtpHost!, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUser, smtpPass)
                };

                var mail = new MailMessage(fromAddress!, toAddress!)
                {
                    Subject = subject,
                    Body = bodyHtml,
                    IsBodyHtml = true
                };

                await client.SendMailAsync(mail);
                Console.WriteLine("Payment success email sent using CMS template + TaxReceiptHtml.");

                return Results.Ok("OK");
            });
        }

        // Simple {{Key}} template replacement
        // Simple {{Key}} template replacement – supports {{Key}}, {{ Key }}, etc.
        private static string ApplyTemplate(string template, IDictionary<string, string?> values)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            var result = template;

            foreach (var kv in values)
            {
                var value = kv.Value ?? string.Empty;

                var patterns = new[]
                {
            "{{" + kv.Key + "}}",
            "{{ " + kv.Key + " }}",
            "{{" + kv.Key + " }}",
            "{{ " + kv.Key + "}}"
        };

                foreach (var pattern in patterns)
                {
                    result = result.Replace(pattern, value);
                }
            }

            return result;
        }


        // Extract DigitalOrder redirect URL from Paystation XML
        private static string ExtractRedirectUrl(string xml)
        {
            var start = xml.IndexOf("<DigitalOrder>", StringComparison.OrdinalIgnoreCase);
            var end = xml.IndexOf("</DigitalOrder>", StringComparison.OrdinalIgnoreCase);
            if (start >= 0 && end > start)
            {
                return xml.Substring(start + "<DigitalOrder>".Length,
                    end - (start + "<DigitalOrder>".Length));
            }
            return string.Empty;
        }
    }
}
