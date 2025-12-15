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
using System.Text.RegularExpressions;

namespace GownApi.Endpoints
{
    public static class PaymentEndpoints
    {
        const string PaystationInitiationUrl = "https://www.paystation.co.nz/direct/paystation.dll";
        const string MerchantId = "617970";
        const string GatewayId = "DEVELOPMENT";

        record PaymentRequest(int Amount, string OrderId);

        public static void MapPaymentEndpoints(this WebApplication app)
        {
            // ---------------- create payment ----------------
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
                    { "pstn_am", request.Amount.ToString() },
                    { "pstn_ms", Guid.NewGuid().ToString() },
                    { "pstn_nr", "t" }
                };

                var response = await httpClient.PostAsync(
                    PaystationInitiationUrl,
                    new FormUrlEncodedContent(values)
                );

                var responseString = await response.Content.ReadAsStringAsync();
                return Results.Ok(new { redirectUrl = ExtractRedirectUrl(responseString) });
            });

            // ---------------- Paystation notify ----------------
            app.MapPost("/notify", async (HttpRequest req, IConfiguration config, GownDb db) =>
            {
                req.EnableBuffering();
                using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
                var raw = await reader.ReadToEndAsync();
                req.Body.Position = 0;

                var doc = XDocument.Parse(raw);
                string? Get(string n) => doc.Root?.Element(n)?.Value;

                int ec = int.TryParse(Get("ec"), out var ecVal) ? ecVal : -1;
                if (ec != 0) return Results.Ok("IGNORED");

                var txnId = Get("PaystationTransactionID") ?? Get("TransactionID") ?? Get("ti");
                var purchaseAmount = (int.TryParse(Get("PurchaseAmount"), out var c) ? c : 0) / 100m;

                var order = await db.orders.OrderByDescending(o => o.Id).FirstOrDefaultAsync();
                if (order == null) return Results.Ok("NO_ORDER");
                string eventTitle = "Graduation Event";

                if (order.CeremonyId.HasValue)
                {
                    var ceremony = await db.ceremonies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == order.CeremonyId.Value);

                    if (ceremony != null && !string.IsNullOrWhiteSpace(ceremony.Name))
                    {
                        eventTitle = ceremony.Name;
                    }
                }


                var orderedItems = await db.orderedItems.Where(o => o.OrderId == order.Id).ToListAsync();
                var skuIds = orderedItems.Select(o => o.SkuId).Distinct().ToList();
                var skus = await db.Sku.Where(s => skuIds.Contains(s.Id)).ToListAsync();
                var itemIds = skus.Select(s => s.ItemId).Distinct().ToList();
                var items = await db.items.Where(i => itemIds.Contains(i.Id)).ToListAsync();

                // -------- build cart rows --------
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

                if (template == null) return Results.Ok("NO_TEMPLATE");

                var invoiceDate = order.OrderDate
                    .ToDateTime(TimeOnly.MinValue)
                    .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

                var values = new Dictionary<string, string?>
                {
                    ["InvoiceNumber"] = order.Id.ToString(),
                    ["InvoiceDate"] = invoiceDate,
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

                };

                var subject = ApplyTemplate(template.SubjectTemplate, values);
                var bodyTop = ApplyTemplate(template.BodyHtml, values);

                // ⬇⬇⬇ 核心：注入 cart rows（唯一来源）⬇⬇⬇
                var receiptHtml = ApplyTemplate(template.TaxReceiptHtml, values);
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
                    int.Parse(config["Smtp:Port"] ?? "587")
                )
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(
                        config["Smtp:Username"],
                        config["Smtp:Password"]
                    )
                };

                var mail = new MailMessage(
                    config["Email:From"]!,
                    config["Email:To"]!
                )
                {
                    Subject = subject,
                    Body = finalBody,
                    IsBodyHtml = true
                };

                await client.SendMailAsync(mail);
                return Results.Ok("OK");
            });
        }

        // ---------------- helpers ----------------

        private static string ApplyTemplate(string template, IDictionary<string, string?> values)
        {
            if (string.IsNullOrEmpty(template)) return "";
            foreach (var kv in values)
            {
                template = Regex.Replace(
                    template,
                    @"\{\{\s*" + Regex.Escape(kv.Key) + @"\s*\}\}",
                    kv.Value ?? "",
                    RegexOptions.IgnoreCase
                );
            }
            return template;
        }

        // ⭐⭐⭐ 唯一允许 CartRows 进入邮件的地方 ⭐⭐⭐
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
                    RegexOptions.IgnoreCase
                );
            }

            return html;
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
