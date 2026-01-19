using DocumentFormat.OpenXml.Drawing.Charts;
using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
using Microsoft.CodeAnalysis.Elfie.Model.Tree;
using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using System.Text.Json.Nodes;

namespace GownApi.Endpoints
{
    public static class OrderEndoints
    {
        public static void MapOrderEnpoints(this WebApplication app)
        {
            app.MapGet("/orders", async (GownDb db) =>
            {
                var result = await db.orders.ToListAsync();
                var resultList = new List<OrderDtoOut>();

                foreach (var res in result)
                {
                    var order = await OrderMapper.ToDtoOut(res, db);
                    resultList.Add(order);
                }
                return Results.Ok(resultList);
            });
            app.MapGet("/api/admin/internal-forms", async (GownDb db) =>
            {
                var list = await db.orders
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new InternalFormListDto
                    {
                        Id = o.Id,
                        Name = (o.FirstName + " " + o.LastName).Trim(),
                        OrderType = o.OrderType ?? "",
                        OrderDate = o.OrderDate,
                        Paid = o.Paid,
                        AmountPaid = o.AmountPaid,
                        Address = o.Address ?? "",
                        ContactNo = !string.IsNullOrWhiteSpace(o.Mobile) ? o.Mobile : (o.Phone ?? ""),
                        OrderNo = o.Reference_no ?? "",
                    })
                    .ToListAsync();

                return Results.Ok(list);
            });

            app.MapGet("/orders/{id}", async (int id, GownDb db, ILogger < Program > logger) =>
            {
                var order = await db.orders.FindAsync(id);

                logger.LogInformation("GET /orders/id called with ID={id}", id);
                if (order is null)
                {
                    logger.LogInformation("order is null");

                    return Results.NotFound();
                }
                var results = await OrderMapper.ToDtoOut(order, db);
                return Results.Ok(results);
            });

            app.MapPost("/orders", async (OrderDto orderDto, GownDb db, ILogger<Program> logger) =>
            {
                var order = OrderMapper.FromDto(orderDto);

                db.orders.Add(order);
                await db.SaveChangesAsync();
                var updatedOrder = await db.orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == order.Id);

                foreach (var item in orderDto.Items) {
                    var itemNew = await db.items.FindAsync(item.ItemId);
                    var skuId = await SkuService.FindSkusAsync(db, item.ItemId, item.SizeId, item.FitId, item.HoodId);

                    logger.LogInformation("Looking for SKU with itemId: {0}, SizeId: {1}, FitId: {2}, HoodId: {3}",
                        item.ItemId, item.SizeId, item.FitId, item.HoodId);

                    if (!skuId.Any()) {
                        db.Sku.Add(new Sku { ItemId = item.ItemId, SizeId = item.SizeId, FitId = item.FitId, HoodId = item.HoodId });
                        await db.SaveChangesAsync();
                        skuId.Add(new Sku { ItemId = item.ItemId, SizeId = item.SizeId, FitId = item.FitId, HoodId = item.HoodId });
                    } else { 
                        logger.LogInformation("Found Sku Id: {0}", skuId[0].Id);
                    }
                    logger.LogInformation("Creating orderted items with order id: {0}, skuid: {1}, quantity: {2}, hire: {3}, cost: {4}",
                        order.Id, skuId[0].Id, item.Quantity, item.Hire, item.Hire ? itemNew.HirePrice : itemNew.BuyPrice);
                    var orderedItems = new OrderedItems
                    {
                        OrderId = order.Id,
                        SkuId = skuId[0].Id, // It stores itemId, should store SkuId instead
                        Quantity = item.Quantity,
                        Hire = item.Hire,
                        Cost = item.Hire ? itemNew.HirePrice : itemNew.BuyPrice
                    };
                    db.orderedItems.Add(orderedItems);
                }

                var result = await db.SaveChangesAsync();

                logger.LogInformation("POST /orders called, result: {id}", result);

                return Results.Created($"/orders/{updatedOrder.Id}", updatedOrder);
            });

            app.MapPut("/orders/{id}", async (int id, Orders updatedOrder, GownDb db, ILogger<Program> logger) =>
            {
                if (id != updatedOrder.Id)
                    return Results.BadRequest("ID in URL and body must match");

                logger.LogInformation("GET /orders/id called with ID={id}", id);
                var order = await db.orders.FindAsync(id);
                if (order is null)
                    return Results.NotFound();

                // Update fields
                order.FirstName = updatedOrder.FirstName;
                order.LastName = updatedOrder.LastName;
                order.Email = updatedOrder.Email;
                order.Address = updatedOrder.Address;
                order.City = updatedOrder.City;
                order.Postcode = updatedOrder.Postcode;
                order.Country = updatedOrder.Country;
                order.Phone = updatedOrder.Phone;
                order.Mobile = updatedOrder.Mobile;
                order.StudentId = updatedOrder.StudentId;
                order.Message = updatedOrder.Message;
                order.Paid = updatedOrder.Paid;
                order.PaymentMethod = updatedOrder.PaymentMethod;
                order.PurchaseOrder = updatedOrder.PurchaseOrder;
                order.OrderDate = updatedOrder.OrderDate;
                order.OrderType = updatedOrder.OrderType;
                order.Note = updatedOrder.Note;
                order.Changes = updatedOrder.Changes;
                order.AmountPaid = updatedOrder.AmountPaid;
                order.AmountOwning = updatedOrder.AmountOwning;
                order.Donation = updatedOrder.Donation;
                order.Freight = updatedOrder.Freight;
                order.Refund = updatedOrder.Refund;
                order.AdminCharges = updatedOrder.AdminCharges;
                order.PayBy = updatedOrder.PayBy;

                await db.SaveChangesAsync();
                return Results.Ok(order);
            });

            app.MapPatch("/orders/{id}", async (int id, OrderDtoUpdate updatedOrder, GownDb db) =>
            {
                var order = await db.orders.FindAsync(id);
                if (order is null) 
                    return Results.NotFound();

                if (updatedOrder.Paid.HasValue)
                    order.Paid = updatedOrder.Paid;

                if (updatedOrder.Status is not null)
                    order.Status = updatedOrder.Status;

                await db.SaveChangesAsync();
                return Results.Ok(order);
            });

            app.MapPost("/api/admin/internal-forms/print-data", async (PdfRequest req, GownDb db) =>
            {
                if (req == null || req.Ids == null || req.Ids.Count == 0)
                    return Results.BadRequest("No ids.");

                var orders = await db.orders
                    .Where(o => req.Ids.Contains(o.Id))
                    .OrderBy(o => o.OrderDate)
                    .ToListAsync();

                var orderIds = orders.Select(o => o.Id).ToList();

                var orderedItems = await db.orderedItems
                    .Where(oi => orderIds.Contains(oi.OrderId))
                    .ToListAsync();

                var skuIds = orderedItems.Select(x => x.SkuId).Distinct().ToList();

                var skus = await db.Sku
                    .Where(s => skuIds.Contains(s.Id))
                    .ToListAsync();

                var itemIds = skus.Select(s => s.ItemId).Distinct().ToList();
                var sizeIds = skus.Where(s => s.SizeId != null).Select(s => s.SizeId!.Value).Distinct().ToList();

                var items = await db.items
                    .Where(i => itemIds.Contains(i.Id))
                    .ToListAsync();

                var sizes = await db.sizes
                    .Where(z => sizeIds.Contains(z.Id))
                    .ToListAsync();

                var skuLookup = skus.ToDictionary(s => s.Id, s => s);
                var itemLookup = items.ToDictionary(i => i.Id, i => i);
                var sizeLookup = sizes.ToDictionary(z => z.Id, z => z);


                var itemsByOrderId = orderedItems
                    .GroupBy(x => x.OrderId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var nzCulture = System.Globalization.CultureInfo.GetCultureInfo("en-NZ");

                var result = orders.Select(o =>
                {
                    var name = $"{o.FirstName} {o.LastName}".Trim();
                    var contact = !string.IsNullOrWhiteSpace(o.Mobile) ? o.Mobile : (o.Phone ?? "");

                    var dto = new InternalFormPrintDto
                    {
                        Id = o.Id,
                        Type = o.OrderType ?? "",
                        Date = o.OrderDate.ToString("yyyy-MM-dd"),
                        Name = name,
                        AddressLine1 = o.Address ?? "",
                        ContactNo = contact,
                        FormDateText = o.OrderDate.ToDateTime(TimeOnly.MinValue).ToString("dddd, d MMMM yyyy", nzCulture),

                        StudentId = o.StudentId,
                        Email = o.Email ?? "",
                    
                        Paid = o.Paid,
                        AmountPaid = o.AmountPaid,
                        WebOrderNo = o.Reference_no,
                        ReceiptNo = "2420",

                        Items = new List<InternalFormPrintItemDto>()
                    };
                 

                    if (itemsByOrderId.TryGetValue(o.Id, out var list))
                    {
                        dto.Items = list.Select(x =>
                        {
                            skuLookup.TryGetValue(x.SkuId, out var sku);

                            var itemName = "";
                            var sizeName = "";
                            var itemType = "";


                            if (sku != null)
                            {
                                if (itemLookup.TryGetValue(sku.ItemId, out var item))
                                    itemName = item?.Name ?? "";
                                    itemType = item?.Type ?? "";

                                if (sku.SizeId != null && sizeLookup.TryGetValue(sku.SizeId.Value, out var size))
                                    sizeName = size.Labelsize ?? size.Size ?? "";
                            }

                            return new InternalFormPrintItemDto
                            {
                                SkuId = x.SkuId,
                                Quantity = x.Quantity,
                                Hire = x.Hire,
                                Cost = x.Cost,

                                ItemName = itemName,
                                SizeName = sizeName,
                                ItemType = itemType
                            };
                        }).ToList();

                    }

                    return dto;
                }).ToList();

                return Results.Ok(result);
            });

        }
    }
}
