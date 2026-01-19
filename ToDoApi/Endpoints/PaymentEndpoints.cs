using GownApi;
using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GownApi.Endpoints
{
    public static class PaymentEndpoints
    {
        const string PaystationInitiationUrl = "https://www.paystation.co.nz/direct/paystation.dll";
        const string MerchantId = "617970";
        const string GatewayId = "DEVELOPMENT";

        record PaymentRequest(int Amount, string OrderNumber);

        public static void MapPaymentEndpoints(this WebApplication app)
        {
            app.MapPost("/api/payment/create-payment", async (
                PaymentRequest request,
                IHttpClientFactory httpClientFactory,
                ILogger<Program> logger) =>
            {
                logger.LogInformation("Request amount {0}, Request order number {1}", request.Amount.ToString(), request.OrderNumber);

                var httpClient = httpClientFactory.CreateClient();
                var values = new Dictionary<string, string>
                {
                    { "paystation", "_empty" },
                    { "pstn_pi", MerchantId },
                    { "pstn_gi", GatewayId },
                    { "pstn_am", request.Amount.ToString() },
                    //{ "pstn_ms", Guid.NewGuid().ToString() },
                    { "pstn_ms", request.OrderNumber },
                    { "pstn_nr", "t" }
                };

                var response = await httpClient.PostAsync(
                    PaystationInitiationUrl,
                    new FormUrlEncodedContent(values)
                );

                var responseString = await response.Content.ReadAsStringAsync();
                return Results.Ok(new { redirectUrl = ExtractRedirectUrl(responseString) });
            });

            //Get response from Paystation notify
            app.MapPost("/notify", async (HttpRequest req, IConfiguration config, GownDb db) =>
            {
                req.EnableBuffering();

                string raw;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true))
                {
                    raw = await reader.ReadToEndAsync();
                    req.Body.Position = 0;
                }

                Console.WriteLine("=================================================");
                Console.WriteLine("Paystation notify received");
                Console.WriteLine($"Time (UTC): {DateTime.UtcNow:O}");
                Console.WriteLine($"Remote IP: {req.HttpContext.Connection.RemoteIpAddress}");
                Console.WriteLine($"Content-Type: {req.ContentType}");
                Console.WriteLine("-------------------------------------------------");
                Console.WriteLine("Headers:");
                foreach (var h in req.Headers)
                {
                    Console.WriteLine($"{h.Key}: {h.Value}");
                }
                Console.WriteLine("-------------------------------------------------");
                Console.WriteLine("Raw XML:");
                Console.WriteLine(raw);
                Console.WriteLine("-------------------------------------------------");

                XDocument doc;
                try
                {
                    doc = XDocument.Parse(raw);
                }
                catch
                {
                    Console.WriteLine("Invalid XML");
                    return Results.Ok("INVALID_XML");
                }

                string? Get(string n) => doc.Root?.Element(n)?.Value;

                int ec = int.TryParse(Get("ec"), out var ecVal) ? ecVal : -1;
                var em = Get("em");
                var txnId = Get("PaystationTransactionID")
                            ?? Get("TransactionID")
                            ?? Get("ti");
                var merchantSession = Get("MerchantSession");
                var txnTime = Get("TransactionTime") ?? Get("DigitalReceiptTime");
                var purchaseAmountCents = int.TryParse(Get("PurchaseAmount"), out var c) ? c : 0;
                var purchaseAmount = purchaseAmountCents / 100m;
                var receiptNumber = Get("ReturnReceiptNumber");

                Console.WriteLine("Parsed fields:");
                Console.WriteLine($"ec: {ec}");
                Console.WriteLine($"em: {em}");
                Console.WriteLine($"txnId: {txnId}");
                Console.WriteLine($"merchantSession: {merchantSession}");
                Console.WriteLine($"txnTime: {txnTime}");
                Console.WriteLine($"purchaseAmount: {purchaseAmount:0.00}");
                Console.WriteLine($"ReturnReceiptNumber: {receiptNumber}");
                Console.WriteLine("=================================================");

                /*if (ec != 0)
                {
                    return Results.Ok("IGNORED");
                }

                var order = await db.orders
                    .OrderByDescending(o => o.Id)
                    .FirstOrDefaultAsync();*/

                var order = await db.orders
    .OrderByDescending(o => o.Id)
    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return Results.Ok("NO_ORDER");
                }

                
                order.Paid = (ec == 0);
                await db.SaveChangesAsync();

                
                if (ec != 0)
                {
                    return Results.Ok("PAYMENT_FAILED_UPDATED");
                }


                if (order == null)
                {
                    return Results.Ok("NO_ORDER");
                }

                var OrderNumber = !string.IsNullOrWhiteSpace(receiptNumber)
                    ? $"ORD{receiptNumber}"
                    : $"ORD{order.Id}";

                string eventTitle = "";
                string ceremonyDate = "";
                string collectionTime = "";

                if (order.CeremonyId.HasValue)
                {
                    var ceremony = await db.ceremonies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ca => ca.Id == order.CeremonyId.Value);

                    if (ceremony != null)
                    {
                        if (!string.IsNullOrWhiteSpace(ceremony.Name))
                        {
                            eventTitle = ceremony.Name;
                        }

                        if (ceremony.CeremonyDate != null)
                        {
                            ceremonyDate = ceremony.CeremonyDate.Value
                                .ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);
                        }

                        if (!string.IsNullOrWhiteSpace(ceremony.CollectionTime))
                        {
                            collectionTime = ceremony.CollectionTime.Trim();
                        }
                    }
                }

                var orderedItems = await db.orderedItems
                    .Where(o => o.OrderId == order.Id)
                    .ToListAsync();

                var skuIds = orderedItems.Select(o => o.SkuId).Distinct().ToList();
                var skus = await db.Sku.Where(s => skuIds.Contains(s.Id)).ToListAsync();
                var itemIds = skus.Select(s => s.ItemId).Distinct().ToList();
                var items = await db.items.Where(i => itemIds.Contains(i.Id)).ToListAsync();

                decimal total = 0m;
                var sbRows = new StringBuilder();

                foreach (var oi in orderedItems)
                {
                    var sku = skus.FirstOrDefault(s => s.Id == oi.SkuId);
                    var item = sku != null ? items.FirstOrDefault(i => i.Id == sku.ItemId) : null;

                    var name = WebUtility.HtmlEncode(item?.Name ?? $"Item {oi.SkuId}");
                    var qty = oi.Quantity;
                    var price = (decimal)oi.Cost;
                    var gst = Math.Round(price * 0.15m * qty, 2);
                    var lineTotal = price * qty;

                    total += lineTotal;

                    sbRows.AppendLine($@"
<tr>
  <td align=""left"">{name}</td>
  <td align=""center"">{qty}</td>
  <td align=""right"">{price:0.00}</td>
  <td align=""right"">{gst:0.00}</td>
  <td align=""right"">{lineTotal:0.00}</td>
</tr>");
                }

                var amountPaid = purchaseAmount == 0 ? total : purchaseAmount;
                var balance = Math.Max(0, total - amountPaid);

                var template = await db.EmailTemplates
                    .AsNoTracking()
                    .SingleOrDefaultAsync(t => t.Name == "PaymentCompleted");

                if (template == null)
                {
                    return Results.Ok("NO_TEMPLATE");
                }

                var OrderDate = order.OrderDate
                    .ToDateTime(TimeOnly.MinValue)
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

                var values = new Dictionary<string, string?>
                {
                    ["OrderNumber"] = OrderNumber,
                    ["OrderDate"] = OrderDate,
                    ["FirstName"] = order.FirstName ?? "",
                    ["LastName"] = order.LastName ?? "",
                    ["Address"] = order.Address ?? "",
                    ["City"] = order.City ?? "",
                    ["Postcode"] = order.Postcode ?? "",
                    ["Country"] = order.Country ?? "",
                    ["StudentId"] = order.StudentId.ToString(),
                    ["Email"] = order.Email ?? "",
                    ["Total"] = total.ToString("0.00"),
                    ["AmountPaid"] = amountPaid.ToString("0.00"),
                    ["BalanceOwing"] = balance.ToString("0.00"),
                    ["EventTitle"] = eventTitle,
                    ["CeremonyDate"] = ceremonyDate
                };

                var subject = ApplyTemplate(template.SubjectTemplate, values);
                var bodyTop = ApplyTemplate(template.BodyHtml, values);

                var receiptHtml = ApplyTemplate(template.TaxReceiptHtml, values);

                receiptHtml = InjectCollectionTimeIntoCollectionRow(receiptHtml, collectionTime);

                receiptHtml = InjectCartRowsIntoCartTable(receiptHtml, sbRows.ToString());

                var receiptInner = ExtractBodyInnerHtml(receiptHtml);

                var finalBody = $@"
<!DOCTYPE html>
<html>
  <body style=""margin:0;padding:0;"">
    {bodyTop}
    <div style=""height:16px;""></div>
    {receiptInner}
  </body>
</html>";

                using var client = new SmtpClient(
                    config["Smtp:Host"]!,
                    int.Parse(config["Smtp:Port"] ?? "587"))
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(
                        config["Smtp:Username"],
                        config["Smtp:Password"])
                };

                var mail = new MailMessage(
                    config["Email:From"]!,
                    config["Email:To"]!)
                {
                    Subject = subject,
                    Body = finalBody,
                    IsBodyHtml = true
                };

                await client.SendMailAsync(mail);
                return Results.Ok("OK");
            });
        }

        private static string ApplyTemplate(string template, IDictionary<string, string?> values)
        {
            if (string.IsNullOrEmpty(template)) return "";
            foreach (var kv in values)
            {
                template = Regex.Replace(
                    template,
                    @"\{\{\s*" + Regex.Escape(kv.Key) + @"\s*\}\}",
                    kv.Value ?? "",
                    RegexOptions.IgnoreCase);
            }
            return template;
        }

        private static string InjectCartRowsIntoCartTable(string html, string rows)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;

            html = Regex.Replace(html, @"\{\{\s*CartRows\s*\}\}", "", RegexOptions.IgnoreCase);

            var pattern =
                @"(<table\b[^>]*data-adh\s*=\s*[""']cart-table[""'][\s\S]*?<tbody\b[^>]*>)([\s\S]*?)(</tbody>)";

            if (Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase))
            {
                return Regex.Replace(
                    html,
                    pattern,
                    m => m.Groups[1].Value + rows + m.Groups[3].Value,
                    RegexOptions.IgnoreCase);
            }

            return html;
        }

        private static string InjectCollectionTimeIntoCollectionRow(string html, string? collectionTime)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;
            if (string.IsNullOrWhiteSpace(collectionTime)) return html;

            var safe = WebUtility.HtmlEncode(collectionTime.Trim())
    .Replace("\r\n", "<br>")
    .Replace("\n", "<br>")
    .Replace("\r", "<br>");


            html = Regex.Replace(
                html,
                @"<div\b[^>]*data-adh\s*=\s*[""']collection-time[""'][^>]*>[\s\S]*?</div>",
                "",
                RegexOptions.IgnoreCase);

            var rowPattern =
                @"(<tr\b[^>]*data-adh\s*=\s*[""']collection-details-row[""'][^>]*>[\s\S]*?<div\b[^>]*style\s*=\s*[""'][^""']*background:#f4f6ff;[\s\S]*?>)([\s\S]*?)(</div>\s*</td>\s*</tr>)";

            if (!Regex.IsMatch(html, rowPattern, RegexOptions.IgnoreCase))
                return html;

            return Regex.Replace(
                html,
                rowPattern,
                m =>
                {
                    var open = m.Groups[1].Value;
                    var middle = m.Groups[2].Value;
                    var close = m.Groups[3].Value;

                    var injected = $@"
<div data-adh=""collection-time"" style=""margin-top:10px; font-size:14px; line-height:1.6;"">
  <strong>{safe}</strong>
</div>";

                    return open + middle + injected + close;
                },
                RegexOptions.IgnoreCase);
        }

        private static string ExtractBodyInnerHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            var lower = html.ToLowerInvariant();
            var start = lower.IndexOf("<body");
            if (start < 0) return html;
            start = lower.IndexOf(">", start) + 1;
            var end = lower.LastIndexOf("</body>");
            return end > start ? html[start..end].Trim() : html;
        }

        private static string ExtractRedirectUrl(string xml)
        {
            var s = xml.IndexOf("<DigitalOrder>", StringComparison.OrdinalIgnoreCase);
            var e = xml.IndexOf("</DigitalOrder>", StringComparison.OrdinalIgnoreCase);
            return s >= 0 && e > s
                ? xml.Substring(s + 14, e - s - 14)
                : "";
        }
    }
}
