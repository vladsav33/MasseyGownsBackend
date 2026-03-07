using GownApi.Model;
using GownApi.Services;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GownApi.Endpoints
{
    public static class EmailEndpoints
    {
        public static void MapEmailEndpoints(this WebApplication app)
        {
            // Internal use: for Azure Function / queue consumer
            app.MapGet("/api/email/render-payment-receipt/{orderId:int}", async (
                int orderId,
                HttpRequest req,
                IConfiguration config,
                GownDb db,
                ILogger<Program> logger) =>
            {
                var expectedKey = config["InternalEmail:Key"];
                var providedKey = req.Headers["X-Internal-Key"].ToString();
                var emailType = req.Headers["X-Email-Type"].ToString();

                if (string.IsNullOrWhiteSpace(expectedKey) || providedKey != expectedKey)
                {
                    logger.LogWarning("RenderPaymentReceipt unauthorized. OrderId={OrderId}", orderId);
                    return Results.Unauthorized();
                }

                if (string.IsNullOrWhiteSpace(emailType))
                {
                    logger.LogWarning("RenderPaymentReceipt missing X-Email-Type. OrderId={OrderId}", orderId);
                    return Results.BadRequest("Missing X-Email-Type header.");
                }

                try
                {
                    var rendered = await RenderReceiptAsync(db, orderId, emailType);

                    if (rendered == null)
                    {
                        return Results.NotFound("Order not found.");
                    }

                    return Results.Ok(new
                    {
                        to = rendered.To,
                        subject = rendered.Subject,
                        html = rendered.Html
                    });
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Email template not found. OrderId={OrderId}, Template={Template}", orderId, emailType);
                    return Results.Problem($"Email template not found: {emailType}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to render payment receipt. OrderId={OrderId}", orderId);
                    return Results.Problem("Failed to render payment receipt.");
                }
            });

            // Front-end use: for PaymentCompleted page
            // Example:
            // GET /orders/123/receipt-html?orderNo=ABC123&email=test@example.com
            app.MapGet("/orders/{orderId:int}/receipt-html", async (
                int orderId,
                string? orderNo,
                string? email,
                GownDb db,
                ILogger<Program> logger) =>
            {
                if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(email))
                {
                    return Results.BadRequest("orderNo and email are required.");
                }

                const string templateName = "PurchaseOrderCompleted";

                try
                {
                    var rendered = await RenderReceiptAsync(db, orderId, templateName);

                    if (rendered == null)
                    {
                        return Results.NotFound("Order not found.");
                    }

                    var orderNoMatched = string.Equals(
                        rendered.OrderNo?.Trim(),
                        orderNo.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                    var emailMatched = string.Equals(
                        rendered.To?.Trim(),
                        email.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                    if (!orderNoMatched || !emailMatched)
                    {
                        logger.LogWarning(
                            "Receipt access denied. OrderId={OrderId}, ProvidedOrderNo={ProvidedOrderNo}, ProvidedEmail={ProvidedEmail}",
                            orderId,
                            orderNo,
                            email);

                        return Results.Unauthorized();
                    }

                    return Results.Ok(new
                    {
                        html = rendered.Html
                    });
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Receipt template not found. OrderId={OrderId}, Template={Template}", orderId, templateName);
                    return Results.Problem($"Email template not found: {templateName}");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to render front-end receipt. OrderId={OrderId}", orderId);
                    return Results.Problem("Failed to render receipt.");
                }
            });
        }
        
        //Shared rendering function
        private static async Task<RenderedReceipt?> RenderReceiptAsync(GownDb db, int orderId, string templateName)
        {
            // 1) Load order
            var order = await db.orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return null;
            }

            // 2) Ceremony info
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
                    eventTitle = ceremony.Name ?? "";

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

            // 3) Order items + totals
            var orderedItems = await db.orderedItems
                .Where(o => o.OrderId == order.Id)
                .ToListAsync();

            var skuIds = orderedItems.Select(o => o.SkuId).Distinct().ToList();
            var skus = skuIds.Count == 0
                ? new List<Sku>()
                : await db.Sku.Where(s => skuIds.Contains(s.Id)).ToListAsync();

            var itemIds = skus.Select(s => s.ItemId).Distinct().ToList();
            var items = itemIds.Count == 0
                ? new List<Items>()
                : await db.items.Where(i => itemIds.Contains(i.Id)).ToListAsync();

            decimal total = 0m;
            var sbRows = new StringBuilder();

            foreach (var oi in orderedItems)
            {
                var sku = skus.FirstOrDefault(s => s.Id == oi.SkuId);
                var item = sku != null ? items.FirstOrDefault(i => i.Id == sku.ItemId) : null;

                var name = WebUtility.HtmlEncode(item?.Name ?? $"Item {oi.SkuId}");
                var qty = oi.Quantity;
                var price = oi.Cost;
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

            var amountPaid = order.AmountPaid.HasValue && order.AmountPaid.Value > 0
                ? order.AmountPaid.Value
                : total;

            var balance = Math.Max(0, total - amountPaid);

            // 4) Load template
            var template = await db.EmailTemplates
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.Name == templateName);

            if (template == null)
            {
                throw new InvalidOperationException($"Email template not found: {templateName}");
            }

            // 5) Build values
            var orderDate = order.OrderDate
                .ToDateTime(TimeOnly.MinValue)
                .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

            var values = new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.ReferenceNo,
                ["OrderDate"] = orderDate,
                ["FirstName"] = order.FirstName ?? "",
                ["LastName"] = order.LastName ?? "",
                ["Address"] = order.Address ?? "",
                ["City"] = order.City ?? "",
                ["Postcode"] = order.Postcode ?? "",
                ["Country"] = order.Country ?? "",
                ["StudentId"] = order.StudentId.ToString(),
                ["Email"] = order.Email ?? "",
                ["Mobile"] = order.Phone ?? "",
                ["Total"] = total.ToString("0.00"),
                ["AmountPaid"] = amountPaid.ToString("0.00"),
                ["BalanceOwing"] = balance.ToString("0.00"),
                ["EventTitle"] = eventTitle,
                ["CeremonyDate"] = ceremonyDate,
                ["GstNumber"] = "41-782-315",
                ["InvoiceNumber"] = order.ReferenceNo
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

            return new RenderedReceipt(
                To: order.Email ?? "",
                Subject: subject,
                Html: finalBody,
                OrderNo: order.ReferenceNo ?? ""
            );
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

        private sealed record RenderedReceipt(
            string To,
            string Subject,
            string Html,
            string OrderNo
        );
    }
}