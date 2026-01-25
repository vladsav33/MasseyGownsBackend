using GownApi;
using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
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
        private const string PaystationInitiationUrl = "https://www.paystation.co.nz/direct/paystation.dll";

        record PaymentRequest(int Amount, string OrderNumber);

        public static void MapPaymentEndpoints(this WebApplication app)
        {
            app.MapPost("/api/payment/create-payment", async (
                PaymentRequest request,
                IHttpClientFactory httpClientFactory,
                IConfiguration config,
                ILogger<Program> logger,
                HttpContext httpContext) =>
            {
                if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.OrderNumber))
                {
                    logger.LogWarning("Invalid create-payment request. AmountCents={AmountCents}, OrderNo={OrderNo}",
                        request.Amount, request.OrderNumber);
                    return Results.BadRequest("Invalid payment request.");
                }

                var paystationId = config["Paystation:PaystationId"] ?? config["Paystation:ClientId"];
                var gatewayId = config["Paystation:GatewayId"];

                if (string.IsNullOrWhiteSpace(paystationId) || string.IsNullOrWhiteSpace(gatewayId))
                {
                    logger.LogError("Paystation config missing. PaystationId/GatewayId not set.");
                    return Results.Problem("Paystation config missing.");
                }

                var orderNo = request.OrderNumber.Trim();
                var merchantSession = orderNo;

                var orderTag = string.IsNullOrWhiteSpace(orderNo) ? "" : $" (Order:{orderNo})";

                
                httpContext.Items["OrderNo"] = orderNo;

              
                using (Serilog.Context.LogContext.PushProperty("OrderNo", orderNo))
                using (Serilog.Context.LogContext.PushProperty("OrderTag", orderTag))
                using (Serilog.Context.LogContext.PushProperty("MerchantSession", merchantSession))
                using (Serilog.Context.LogContext.PushProperty("AmountCents", request.Amount))
                using (logger.BeginScope(new Dictionary<string, object?>
                {
                    ["OrderNo"] = orderNo,
                    ["OrderTag"] = orderTag,
                    ["MerchantSession"] = merchantSession,
                    ["AmountCents"] = request.Amount
                }))
                {
                    logger.LogInformation("Paystation init start. PaystationId={PaystationId}, GatewayId={GatewayId}",
                        paystationId, gatewayId);

                    try
                    {
                        var httpClient = httpClientFactory.CreateClient();

                        var values = new Dictionary<string, string>
                        {
                            { "paystation", "_empty" },
                            { "pstn_pi", paystationId },
                            { "pstn_gi", gatewayId },
                            { "pstn_am", request.Amount.ToString(CultureInfo.InvariantCulture) },
                            { "pstn_ms", merchantSession },
                            { "pstn_nr", "t" }
                        };

                        var response = await httpClient.PostAsync(
                            PaystationInitiationUrl,
                            new FormUrlEncodedContent(values)
                        );

                        var responseString = await response.Content.ReadAsStringAsync();
                        var redirectUrl = ExtractRedirectUrl(responseString);

                        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(redirectUrl))
                        {
                            logger.LogWarning("Paystation init failed. StatusCode={StatusCode}, RedirectUrlEmpty={RedirectEmpty}, BodySnippet={BodySnippet}",
                                (int)response.StatusCode,
                                string.IsNullOrWhiteSpace(redirectUrl),
                                SafeSnippet(responseString, 400));

                            return Results.Problem("Failed to initiate payment.");
                        }

                        logger.LogInformation("Paystation init success. RedirectUrl={RedirectUrl}", redirectUrl);
                        return Results.Ok(new { redirectUrl });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Paystation init exception.");
                        return Results.Problem("Failed to initiate payment.");
                    }
                }
            });

            app.MapPost("/notify", async (
                HttpRequest req,
                IConfiguration config,
                GownDb db,
                ILogger<Program> logger) =>
            {
                req.EnableBuffering();

                string raw;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true))
                {
                    raw = await reader.ReadToEndAsync();
                    req.Body.Position = 0;
                }

                XDocument doc;
                try
                {
                    doc = XDocument.Parse(raw);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Paystation notify invalid XML. ContentType={ContentType}, RemoteIP={RemoteIP}, BodySnippet={BodySnippet}",
                        req.ContentType,
                        req.HttpContext.Connection.RemoteIpAddress?.ToString(),
                        SafeSnippet(raw, 600));

                    return Results.Ok("INVALID_XML");
                }

                string? Get(string n) => doc.Root?.Element(n)?.Value;

                int ec = int.TryParse(Get("ec"), out var ecVal) ? ecVal : -1;
                var em = Get("em");

                var txnId = Get("PaystationTransactionID")
                            ?? Get("TransactionID")
                            ?? Get("ti");

                var merchantSession = Get("MerchantSession")?.Trim();
                var orderNo = merchantSession;

                var orderTag = string.IsNullOrWhiteSpace(orderNo) ? "" : $" (Order:{orderNo})";

                
                if (!string.IsNullOrWhiteSpace(orderNo))
                {
                    req.HttpContext.Items["OrderNo"] = orderNo;
                }

                var txnTime = Get("TransactionTime") ?? Get("DigitalReceiptTime");
                var purchaseAmountCents = int.TryParse(Get("PurchaseAmount"), out var cents) ? cents : 0;
                var purchaseAmount = purchaseAmountCents / 100m;
                var receiptNumber = Get("ReturnReceiptNumber");

                using (Serilog.Context.LogContext.PushProperty("OrderNo", orderNo ?? ""))
                using (Serilog.Context.LogContext.PushProperty("OrderTag", orderTag))
                using (Serilog.Context.LogContext.PushProperty("MerchantSession", merchantSession ?? ""))
                using (Serilog.Context.LogContext.PushProperty("PaystationTxnId", txnId ?? ""))
                using (Serilog.Context.LogContext.PushProperty("PaystationEC", ec))
                using (Serilog.Context.LogContext.PushProperty("AmountCents", purchaseAmountCents))
                using (Serilog.Context.LogContext.PushProperty("ReceiptNo", receiptNumber ?? ""))
                using (logger.BeginScope(new Dictionary<string, object?>
                {
                    ["OrderNo"] = orderNo ?? "",
                    ["OrderTag"] = orderTag,
                    ["MerchantSession"] = merchantSession ?? "",
                    ["PaystationTxnId"] = txnId ?? "",
                    ["PaystationEC"] = ec,
                    ["AmountCents"] = purchaseAmountCents,
                    ["ReceiptNo"] = receiptNumber ?? ""
                }))
                {
                    logger.LogInformation("Paystation notify received. ec={EC}, em={EM}, txnTime={TxnTime}, amount={Amount}, receipt={Receipt}",
                        ec, em, txnTime, purchaseAmount, receiptNumber);

                    if (string.IsNullOrWhiteSpace(merchantSession))
                    {
                        logger.LogWarning("Paystation notify missing MerchantSession. BodySnippet={BodySnippet}", SafeSnippet(raw, 600));
                        return Results.Ok("NO_MERCHANT_SESSION");
                    }

                    var order = await db.orders
                        .FirstOrDefaultAsync(o => o.ReferenceNo == merchantSession);

                    if (order == null)
                    {
                        logger.LogWarning("No order found by ReferenceNo(MerchantSession)={MerchantSession}", merchantSession);
                        return Results.Ok("NO_ORDER_BY_REFERENCE");
                    }

                    using (Serilog.Context.LogContext.PushProperty("OrderId", order.Id))
                    using (logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["OrderId"] = order.Id
                    }))
                    {
                        order.Paid = (ec == 0);

                        if (purchaseAmountCents > 0)
                        {
                            order.AmountPaid = (float)(purchaseAmountCents / 100m);
                        }

                        try
                        {
                            await db.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to save payment status to DB.");
                            return Results.Ok("DB_SAVE_FAILED");
                        }

                        if (ec != 0)
                        {
                            logger.LogWarning("Payment failed (ec!=0). Updated order as unpaid. ec={EC}, em={EM}", ec, em);
                            return Results.Ok("PAYMENT_FAILED_UPDATED");
                        }

                       

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
                            logger.LogWarning("Email template not found. TemplateName=PaymentCompleted");
                            return Results.Ok("NO_TEMPLATE");
                        }

                        var orderDate = order.OrderDate
                            .ToDateTime(TimeOnly.MinValue)
                            .ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

                        var values = new Dictionary<string, string?>
                        {
                            ["OrderNumber"] = orderNo ?? "",
                            ["OrderDate"] = orderDate,
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

                        try
                        {
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

                            logger.LogInformation("Payment success + email sent. Total={Total}, AmountPaid={AmountPaid}, Balance={Balance}",
                                total, amountPaid, balance);

                            return Results.Ok("OK");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Payment success but email sending failed.");
                            return Results.Ok("EMAIL_FAILED");
                        }
                    }
                }
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

        private static string SafeSnippet(string? text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= maxLen ? text : text[..maxLen] + "...";
        }
    }
}