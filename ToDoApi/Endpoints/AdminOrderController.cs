using DocumentFormat.OpenXml.Office2010.PowerPoint;
using DocumentFormat.OpenXml.Wordprocessing;
using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class AdminOrderController
    {
        public static void AdminOrderEndpoints(this WebApplication app)
        {
            _ = app.MapGet("/admin/hirednotgraduated/{id}", async (int id, GownDb db) =>
            {
                var sql = @"
                            SELECT
                                o.id,
                                o.student_id, 
                                o.last_name, 
                                o.first_name,
                                d.name as degree,
                                c.id_code as ceremony_name,
                                oi_selected.hood_name as hood_name,
                                o.reference_no, 
                                o.phone as mobile
                            FROM orders o
                            JOIN degrees d 
                                ON o.degree_id = d.id
                            JOIN ceremonies c
                                ON c.id = o.ceremony_id
                            LEFT JOIN LATERAL (
                                SELECT oi.*, ht.name AS hood_name
                                FROM ordered_items oi
                                LEFT JOIN sku sk ON sk.id = oi.sku_id
                                LEFT JOIN hood_type ht ON ht.id = sk.hood_id
                                WHERE oi.order_id = o.id
                                ORDER BY 
                                    CASE WHEN ht.id IS NOT NULL THEN 0 ELSE 1 END
                                LIMIT 1
                            ) oi_selected ON true
                            WHERE o.ceremony_id = @id
                            AND NOT o.staff
                            AND NOT EXISTS (
                                SELECT 1 
                                FROM ceremonies c
                                JOIN session_ceremony sc ON sc.ceremony_id = c.id
                                JOIN ceremony_import ci ON ci.ceremony_name = sc.session
                                WHERE c.id = @id 
                                  AND ci.student_id::numeric = o.student_id)
                            ORDER by o.last_name, o.first_name";

                var param = new NpgsqlParameter("@id", id);
                var result = await db.hiredNotGraduated.FromSqlRaw(sql, param).ToListAsync();
                return Results.Ok(result);
            });

            _ = app.MapGet("/admin/graduatednothired/{id}", async(int id, GownDb db) =>
            {
                var sql = @"
                            SELECT ci.id, ci.student_id, ci.surname as last_name, ci.forename as first_name, ci.program_code as degree, c.id_code as ceremony_name, ci.program_desc as hood_name, ci.mobile
                            FROM ceremony_import ci
                            JOIN session_ceremony sc ON sc.session=ci.ceremony_name
                            JOIN ceremonies c ON c.id = sc.ceremony_id
                            WHERE c.id = @id
                            AND NOT EXISTS (
	                            SELECT 1
	                            FROM orders o
	                            WHERE o.student_id = CAST (ci.student_id AS NUMERIC)
	                            AND o.ceremony_id = @id
                            )
                            ORDER BY last_name, first_name";
                var param = new NpgsqlParameter("@id", id);
                var result = await db.graduatedNotHired.FromSqlRaw(sql, param).ToListAsync();

                return Results.Ok(result);
            });

            _ = app.MapGet("/admin/ordersbyceremony/{id}", async (int id, GownDb db) =>
            {
                var sql = @"SELECT o.id as id, c.id_code, o.first_name, o.last_name, o.head_size, o.height, o.email, o.address, o.city, o.payment_ec, o.payment_em,o.postcode, o.country, o.phone,
                                      o.order_amount, o.student_id, o.message, o.paid, o.payment_method, o.purchase_order, o.order_date, c.id as ceremony_id,
                                      c.name as ceremony, o.degree_id, o.order_type, o.note, o.changes, o.pack_note, o.amount_paid,
                                      o.amount_owning, o.donation, c.account_code, o.freight, o.refund, o.admin_charges, o.pay_by, o.status, o.reference_no,
                                      o.refund_status_code, o.refund_txn_id, o.refunded_amount, o.refund_initiated_at, o.payment_txn_id, o.refund_last_ec, o.refund_last_em, o.refund_email_sent_at, o.staff
                                      FROM orders o
                                      LEFT JOIN ceremonies c
                                      ON o.ceremony_id = c.id
                                      WHERE o.ceremony_id = @id AND o.reference_no is not null
                                      ORDER BY o.reference_no DESC";
                var param = new NpgsqlParameter("@id", id);
                var result = await db.orderGets.FromSqlRaw(sql, param).ToListAsync();

                var resultList = new List<OrderDtoOut>();

                foreach (var res in result)
                {
                    var order = await OrderMapper.ToDtoOut(res, db);
                    resultList.Add(order);
                }

                return Results.Ok(resultList);
            });

            _ = app.MapPost("/admin/indorder", async (List<OrderDtoIn> ordersDto, GownDb db, ILogger<Program> logger) =>
            {
                //logger.LogInformation("POST /admin/indorder called with {Count} orders, Body={@ordersDto}", ordersDto.Count, ordersDto);

                if (ordersDto is null || ordersDto.Count == 0)
                    return Results.BadRequest("No orders supplied.");


                // All-or-nothing: if any order fails, none are saved.
                await using var transaction = await db.Database.BeginTransactionAsync();
                try
                {
                    // Each order paired with the ordered items resolved from its DTO (OrderId set after insert)
                    var orders = new List<(Orders Order, List<OrderedItems> Items)>(ordersDto.Count);

                    // Ceremonies with an ImportName, used to match each order's University
                    var ceremonies = await db.ceremonies
                        .Where(c => c.Id != null && c.NameImport != null && c.NameImport != "")
                        .OrderBy(c => c.Id)
                        .Select(c => new { Id = c.Id!.Value, c.NameImport, c.InstitutionCode })
                        .ToListAsync();

                    var skipped = 0;
                    foreach (var orderDto in ordersDto)
                    {
                        var university = orderDto.University?.Trim();
                        var matches = string.IsNullOrEmpty(university)
                            ? []
                            : ceremonies
                                .Where(c => c.NameImport!.Contains(university, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                        if (matches.Count == 0)
                        {
                            logger.LogWarning("Order {OrderNumber} skipped: University '{University}' does not match any ceremony ImportName", orderDto.OrderNumber, orderDto.University);
                            skipped++;
                            continue;
                        }

                        if (matches.Count > 1)
                            logger.LogWarning("Order {OrderNumber}: University '{University}' matches {Count} ceremonies ({Ids}); using {CeremonyId}",
                                orderDto.OrderNumber, orderDto.University, matches.Count, string.Join(", ", matches.Select(m => m.Id)), matches[0].Id);

                        var ceremonyId = matches[0].Id;
                        var instituteCode = matches[0].InstitutionCode;

                        var orderedItems = new List<OrderedItems>();

                        AddIfNotNull(orderedItems, await ProcessTrencherAsync(orderDto, db, logger));
                        orderedItems.AddRange(await ProcessGownAsync(orderDto, db, logger));
                        AddIfNotNull(orderedItems, await ProcessHoodAsync(orderDto, db, logger, instituteCode));
                        AddIfNotNull(orderedItems, await ProcessStoleAsync(orderDto, db, logger));

                        var order = await OrderMapper.ToEntity(orderDto, ceremonyId, db);

                        //logger.LogInformation("Note length: {Length}", order.Note?.Length ?? 0);

                        orders.Add((order, orderedItems));
                    }

                    // Insert the orders first so the database generates their ids
                    await db.orders.AddRangeAsync(orders.Select(o => o.Order));
                    await db.SaveChangesAsync();

                    // Now link each ordered item to its newly created order id
                    var itemCount = 0;
                    foreach (var (order, orderedItems) in orders)
                    {
                        foreach (var orderedItem in orderedItems)
                        {
                            orderedItem.OrderId = order.Id;
                            db.orderedItems.Add(orderedItem);
                            itemCount++;
                        }
                    }

                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    logger.LogInformation("POST /admin/indorder created {Count} orders with {ItemCount} ordered items, skipped {Skipped}", orders.Count, itemCount, skipped);

                    return Results.Created("/admin/orders", new
                    {
                        message = "Orders created successfully",
                        inserted = orders.Count,
                        skipped
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    logger.LogError(ex, "Error creating orders in POST /admin/indorder");
                    return Results.Problem(
                        title: "An error occurred while creating the orders.",
                        detail: ex.InnerException?.Message ?? ex.Message);
                }
            });
        }

        private static void AddIfNotNull(List<OrderedItems> list, OrderedItems? orderedItem)
        {
            if (orderedItem != null)
                list.Add(orderedItem);
        }

        // Builds an ordered item for the SKU. OrderId is filled in once the order has been inserted.
        private static OrderedItems ToOrderedItem(Items item, Sku sku) => new OrderedItems
        {
            SkuId = sku.Id,
            Quantity = 1,
            Hire = item.IsHiring,
            Cost = (item.IsHiring ? item.HirePrice : item.BuyPrice) ?? 0
        };

        private static async Task<OrderedItems?> ProcessTrencherAsync(OrderDtoIn orderDto, GownDb db, ILogger logger)
        {

            if (orderDto.Trencher is { Count: > 0 })
            {
                Items? item = null;
                Hats? size = null;
                if (orderDto.Trencher.TryGetValue("Name", out var name) && name != null)
                {
                    //logger.LogInformation("Order {OrderNumber} Trencher Name={Name}", orderDto.OrderNumber, name);

                    var itemName = name?.Trim().ToLower();
                    item = string.IsNullOrEmpty(itemName)
                                                    ? null
                                                    : await db.items.FirstOrDefaultAsync(i => i.NameImport.ToLower().Contains(itemName));

                    if (item is null)
                        logger.LogWarning("Order {OrderNumber} Trencher item '{Name}' not found", orderDto.OrderNumber, name);
                    //else
                    //    logger.LogInformation("Order {OrderNumber} Trencher item found: Id={ItemId}, Name={ItemName}", orderDto.OrderNumber, item.Id, item.Name);
                }

                if (orderDto.Trencher.TryGetValue("Size", out var hatSize) && hatSize != null && item != null)
                {
                    var itemSize = hatSize?.Trim().ToLower();
                    size = string.IsNullOrEmpty(itemSize)
                        ? null
                        : await db.hats.FirstOrDefaultAsync(s => s.Size.ToLower() == itemSize && s.ItemId == item.Id);
                    //logger.LogInformation("Order {OrderNumber} Trencher Size={Size} Size Id={id}, Size Name={Name}", orderDto.OrderNumber, hatSize, size?.Id, size?.Size);
                }
                if (item != null && size != null)
                {
                    var skus = await SkuService.FindSkusAsync(db, item.Id, size.Id);
                    Sku sku;
                    if (skus.Count > 0)
                    {
                        sku = skus[0];
                        //logger.LogInformation("SKU found for Order {OrderNumber} Trencher: ItemId={ItemId}, SizeId={SizeId}, SKU Id={SkuId}", orderDto.OrderNumber, item.Id, size.Id, skus[0].Id);
                    }
                    else
                    {
                        logger.LogWarning("SKU not found for Order {OrderNumber} Trencher: ItemId={ItemId}, SizeId={SizeId}", orderDto.OrderNumber, item.Id, size.Id);
                        // Optionally, you could create a new SKU if it doesn't exist
                        sku = await SkuService.InsertSkuAsync(db, item.Id, size.Id, null, null, size.Id);
                        logger.LogInformation("Inserted new SKU for Order {OrderNumber} Trencher: ItemId={ItemId}, SizeId={SizeId}", orderDto.OrderNumber, item.Id, size.Id);
                    }
                    return ToOrderedItem(item, sku);
                }

            }
            else
            {
                logger.LogInformation("Order {OrderNumber} Trencher: <empty>", orderDto.OrderNumber);
            }
            return null;
        }

        private static async Task<List<OrderedItems>> ProcessGownAsync(OrderDtoIn orderDto, GownDb db, ILogger logger)
        {
            var orderedItems = new List<OrderedItems>();

            if (orderDto.Gown is { Length: > 0 })
            {
                foreach (var gown in orderDto.Gown)
                {
                    if (gown.TryGetValue("Type", out var type) && type != null && type.Trim().ToLower().Contains("set"))
                        AddIfNotNull(orderedItems, await RetrieveSetAsync(gown, orderDto, db, logger));
                    else if (type != null)
                        AddIfNotNull(orderedItems, await RetrieveGownAsync(gown, orderDto, db, logger));
                }
            }
            else
            {
                //logger.LogInformation("Order {OrderNumber} Gown: <empty>", orderDto.OrderNumber);
            }
            return orderedItems;
        }

        private static async Task<OrderedItems?> RetrieveGownAsync(Dictionary<string, string?> gown, OrderDtoIn orderDto, GownDb db, ILogger logger)
        {
            Items? item = null;
            Sizes? size = null;
            Fit? fit = null;

            if (gown.TryGetValue("Type", out var type) && type != null)
            {
                //logger.LogInformation("Order {OrderNumber} Gown Type={Type}", orderDto.OrderNumber, type);

                var itemName = type?.Trim().ToLower();
                item = string.IsNullOrEmpty(itemName)
                                                ? null
                                                : await db.items.FirstOrDefaultAsync(i => i.NameImport.ToLower() == itemName);

                //if (item is null)
                //    logger.LogWarning("Order {OrderNumber} Gown item '{Name}' not found", orderDto.OrderNumber, type);
                //else
                //    logger.LogInformation("Order {OrderNumber} Gown item found: Id={ItemId}, Name={ItemName}", orderDto.OrderNumber, item.Id, item.Name);
            }

            if (gown.TryGetValue("FitType", out var gownFit) && gownFit != null)
            {
                var itemFit = gownFit?.Trim().ToLower();
                fit = string.IsNullOrEmpty(itemFit)
                    ? null
                    : await db.fit.FirstOrDefaultAsync(f => f.FitType.ToLower().Contains(itemFit));

                if (fit == null)
                {
                    logger.LogWarning("Order {OrderNumber} Gown fit '{FitType}' not found", orderDto.OrderNumber, gownFit);
                }

                //logger.LogInformation("Order {OrderNumber} Gown Fit={Fit} Fit Id={id}, Fit Name={Name}", orderDto.OrderNumber, gownFit, fit?.Id, fit?.FitType);
            }

            if (gown.TryGetValue("Size", out var gownSize) && gownSize != null && fit != null && item != null)
            {
                var itemSize = gownSize?.Trim().ToLower();
                size = string.IsNullOrEmpty(itemSize)
                    ? null
                    : await db.sizes.FirstOrDefaultAsync(s => s.Size.ToLower() == itemSize && s.FitId == fit.Id && s.ItemId == item.Id);

                if (size == null)
                {
                    logger.LogWarning("Order {OrderNumber} Gown size '{Size}' not found for fit '{FitType}'", orderDto.OrderNumber, gownSize, gownFit);
                }

                //logger.LogInformation("Order {OrderNumber} Gown Size={Size} Size Id={id}, Size Name={Name}", orderDto.OrderNumber, gownSize, size?.Id, size?.Size);
            }

            if (item != null && size != null && fit != null)
            {
                var skus = await SkuService.FindSkusAsync(db, item.Id, size.Id, fit.Id);
                Sku sku;
                if (skus.Count > 0)
                {
                    sku = skus[0];
                    //logger.LogInformation("SKU found for Order {OrderNumber} Gown: ItemId={ItemId}, SizeId={SizeId}, FitId={FitId}, SKU Id={SkuId}", orderDto.OrderNumber, item.Id, size.Id, fit.Id, skus[0].Id);
                }
                else
                {
                    logger.LogWarning("SKU not found for Order {OrderNumber} Gown: ItemId={ItemId}, SizeId={SizeId}, FitId={FitId}", orderDto.OrderNumber, item.Id, size.Id, fit.Id);
                    // Optionally, you could create a new SKU if it doesn't exist
                    sku = await SkuService.InsertSkuAsync(db, item.Id, size.Id, fit.Id);
                    logger.LogInformation("Inserted new SKU for Order {OrderNumber} Gown: ItemId={ItemId}, SizeId={SizeId}, FitId={FitId}", orderDto.OrderNumber, item.Id, size.Id, fit.Id);
                }
                return ToOrderedItem(item, sku);
            }
            return null;
        }

        private static async Task<OrderedItems?> RetrieveSetAsync(Dictionary<string, string?> gown, OrderDtoIn orderDto, GownDb db, ILogger logger)
        {
            Items? item = null;
            Sizes? size = null;
            Hats? hat = null;
            HoodType? hood = null;

            if (gown.TryGetValue("Type", out var type) && type != null)
            {
                //logger.LogInformation("Order {OrderNumber} Gown Type={Type}", orderDto.OrderNumber, type);

                var itemName = type?.Trim().ToLower();
                if (itemName == "doctoral set new")
                    itemName = "doctoral degree set";

                item = string.IsNullOrEmpty(itemName)
                                                ? null
                                                : await db.items.FirstOrDefaultAsync(i => i.NameImport.ToLower().Contains(itemName));

                if (item is null)
                    logger.LogWarning("Order {OrderNumber} Set item '{Name}' not found", orderDto.OrderNumber, type);
                else
                    logger.LogInformation("Order {OrderNumber} Set item found: Id={ItemId}, Name={ItemName}", orderDto.OrderNumber, item.Id, item.Name);
            }

            if (gown.TryGetValue("SetHatSize", out var setHat) && setHat != null && item != null)
            {
                var itemHat = setHat?.Trim().ToLower();
                hat = string.IsNullOrEmpty(itemHat)
                    ? null
                    : await db.hats.FirstOrDefaultAsync(h => h.Size.ToLower().Contains(itemHat) && h.ItemId == item.Id);

                if (hat == null)
                {
                    logger.LogWarning("Order {OrderNumber} Set hat '{HatSize}' not found", orderDto.OrderNumber, setHat);
                }

                //logger.LogInformation("Order {OrderNumber} Gown Fit={Fit} Fit Id={id}, Fit Name={Name}", orderDto.OrderNumber, gownFit, fit?.Id, fit?.FitType);
            }

            if (gown.TryGetValue("Size", out var gownSize) && gownSize != null && item != null)
            {
                var itemSize = gownSize?.Trim().ToLower();
                size = string.IsNullOrEmpty(itemSize)
                    ? null
                    : await db.sizes.FirstOrDefaultAsync(s => s.Size.ToLower() == itemSize && s.ItemId == item.Id);

                if (size == null)
                {
                    logger.LogWarning("Order {OrderNumber} Set size '{Size}' not found'", orderDto.OrderNumber, gownSize);
                }

                //logger.LogInformation("Order {OrderNumber} Gown Size={Size} Size Id={id}, Size Name={Name}", orderDto.OrderNumber, gownSize, size?.Id, size?.Size);
            }

            if (gown.TryGetValue("SetHood", out var setHood) && setHood != null)
            {
                var itemHood = setHood?.Trim().ToLower();
                hood = string.IsNullOrEmpty(itemHood)
                    ? null
                    : await db.hood_type.FirstOrDefaultAsync(s => s.Name.ToLower() == itemHood);

                if (hood == null)
                {
                    logger.LogWarning("Order {OrderNumber} Set hood '{Hood}' not found'", orderDto.OrderNumber, hood);
                }

                //logger.LogInformation("Order {OrderNumber} Gown Size={Size} Size Id={id}, Size Name={Name}", orderDto.OrderNumber, gownSize, size?.Id, size?.Size);
            }

            if (item != null && size != null && hat != null && hood != null)
            {
                var skus = await SkuService.FindSkusAsync(db, item.Id, size.Id, null, hood.Id, hat.Id);
                Sku sku;
                if (skus.Count > 0)
                {
                    sku = skus[0];
                    //logger.LogInformation("SKU found for Order {OrderNumber} Gown: ItemId={ItemId}, SizeId={SizeId}, FitId={FitId}, SKU Id={SkuId}", orderDto.OrderNumber, item.Id, size.Id, fit.Id, skus[0].Id);
                }
                else
                {
                    logger.LogWarning("SKU not found for Order {OrderNumber} Set: ItemId={ItemId}, SizeId={SizeId}, HatId={HatId}, HoodId={HoodId}", orderDto.OrderNumber, item.Id, size.Id, hat.Id, hood.Id);
                    // Optionally, you could create a new SKU if it doesn't exist
                    sku = await SkuService.InsertSkuAsync(db, item.Id, size.Id, null, hood.Id, hat.Id);
                    logger.LogInformation("Inserted new SKU for Order {OrderNumber} Set: ItemId={ItemId}, SizeId={SizeId}, HatId={HatId}, HoodId={HoodId}", orderDto.OrderNumber, item.Id, size.Id, hat.Id, hood.Id);
                }
                return ToOrderedItem(item, sku);
            }
            else
            {
                logger.LogWarning("Order {OrderNumber} Set: Missing item, size, or hat. ItemId={ItemId}, SizeId={SizeId}, HatId={HatId}", orderDto.OrderNumber, item?.Id, size?.Id, hat?.Id);
            }
            return null;
        }

        private static async Task<OrderedItems?> ProcessHoodAsync(OrderDtoIn orderDto, GownDb db, ILogger logger, string? instituteCode)
        {
            if (orderDto.Hood is { Count: > 0 })
            {
                if (orderDto.Hood.TryGetValue("Type", out var typeHood) && typeHood != null && typeHood.Trim().ToLower().Contains("phd"))
                {
                    return await RetrievePhdHoodAsync(orderDto.Hood, orderDto, db, logger);
                } else if (orderDto.Hood.TryGetValue("Type", out var typePhd) && typePhd != null && typePhd.Trim().ToLower().Contains("hood"))
                {
                    return await RetrieveHoodAsync(orderDto.Hood, orderDto, db, logger, instituteCode);
                }
            }
            return null;
        }

        private static async Task<OrderedItems?> RetrieveHoodAsync(Dictionary<string, string?> hoodIn, OrderDtoIn orderDto, GownDb db, ILogger logger, string? instituteCode)
        {
            HoodType? hood = null;
            Items? item = null;

            if (hoodIn.TryGetValue("Type", out var type) && type != null)
            {
                //logger.LogInformation("Order {OrderNumber} Gown Type={Type}", orderDto.OrderNumber, type);

                var itemName = type?.Trim().ToLower();

                item = string.IsNullOrEmpty(itemName)
                                                ? null
                                                : await db.items.FirstOrDefaultAsync(i => i.NameImport.Trim().ToLower().Contains(itemName));

                if (item is null)
                    logger.LogWarning("Order {OrderNumber} Hood type '{Name}' not found", orderDto.OrderNumber, type);
            }

            //if (orderDto.Hood.TryGetValue("Name", out var setHood) && setHood != null)
            //{
            //    var itemHood = setHood?.Trim().ToLower();
            //    hood = string.IsNullOrEmpty(itemHood)
            //        ? null
            //        : await db.hood_type.FirstOrDefaultAsync(s => s.NameImport.Trim().ToLower().Contains(itemHood) && s.Institution == "MU");

            //    if (hood == null)
            //    {
            //        //logger.LogWarning("Order {OrderNumber} Hood name '{Hood}' not found'", orderDto.OrderNumber, itemHood);
            //    }

            //    //logger.LogInformation("Order {OrderNumber} Hood Name={Name}", orderDto.OrderNumber, setHood);
            //}

            // If the hood was not found by Name, try to find it by ShortName
            if (hood == null && orderDto.Hood.TryGetValue("ShortName", out var setHoodShort) && setHoodShort != null)
            {
                var itemHood = setHoodShort?.Trim().ToLower();
                hood = string.IsNullOrEmpty(itemHood)
                    ? null
                    : await db.hood_type.FirstOrDefaultAsync(s => s.NameImport.Trim().ToLower() == itemHood && s.Institution == instituteCode);

                if (hood == null)
                {
                    logger.LogWarning("Order {OrderNumber} Hood Short name '{Hood}' not found'", orderDto.OrderNumber, itemHood);
                }

                //logger.LogInformation("Order {OrderNumber} Gown Size={Size} Size Id={id}, Size Name={Name}", orderDto.OrderNumber, gownSize, size?.Id, size?.Size);
            }

            if (item != null && hood != null)
            {
                var skus = await SkuService.FindSkusAsync(db, item.Id, null, null, hood.Id);
                Sku sku;
                if (skus.Count > 0)
                {
                    sku = skus[0];
                    //logger.LogInformation("SKU found for Order {OrderNumber} Gown: ItemId={ItemId}, SizeId={SizeId}, FitId={FitId}, SKU Id={SkuId}", orderDto.OrderNumber, item.Id, size.Id, fit.Id, skus[0].Id);
                }
                else
                {
                    logger.LogWarning("SKU not found for Order {OrderNumber} Hood: ItemId={ItemId}, HoodId={HoodId}", orderDto.OrderNumber, item.Id, hood.Id);
                    // Optionally, you could create a new SKU if it doesn't exist
                    sku = await SkuService.InsertSkuAsync(db, item.Id, null, null, hood.Id);
                    logger.LogInformation("Inserted new SKU for Order {OrderNumber} Hood: ItemId={ItemId}, HoodId={HoodId}", orderDto.OrderNumber, item.Id, hood.Id);
                }
                return ToOrderedItem(item, sku);
            }
            else
            {
                //logger.LogWarning("Order {OrderNumber} Hood: Missing item or hood. ItemId={ItemId}, HoodId={HoodId}", orderDto.OrderNumber, item?.Id, hood?.Id);
            }
            return null;
        }

        private static async Task<OrderedItems?> RetrievePhdHoodAsync(Dictionary<string, string?> hoodIn, OrderDtoIn orderDto, GownDb db, ILogger logger)
        {
            Sizes? size = null;
            Items? item = null;

            if (hoodIn.TryGetValue("Type", out var type) && type != null)
            {
                //logger.LogInformation("Order {OrderNumber} Gown Type={Type}", orderDto.OrderNumber, type);

                var itemName = type?.Trim().ToLower();

                item = string.IsNullOrEmpty(itemName)
                                                ? null
                                                : await db.items.FirstOrDefaultAsync(i => i.Name.ToLower() == "hood - phd");

                if (item is null)
                    logger.LogWarning("Order {OrderNumber} PhD Hood type '{Name}' not found", orderDto.OrderNumber, type);
            }

            if (orderDto.Hood.TryGetValue("Size", out var hoodSize) && hoodSize != null)
            {
                var itemSize = hoodSize?.Trim().ToLower();
                size = string.IsNullOrEmpty(itemSize)
                    ? null
                    : await db.sizes.FirstOrDefaultAsync(s => s.Size.Trim().ToLower() == itemSize);

                if (size == null)
                {
                    //logger.LogWarning("Order {OrderNumber} Hood name '{Hood}' not found'", orderDto.OrderNumber, itemHood);
                }

                //logger.LogInformation("Order {OrderNumber} Gown Size={Size} Size Id={id}, Size Name={Name}", orderDto.OrderNumber, gownSize, size?.Id, size?.Size);
            }

            if (item != null && size != null)
            {
                var skus = await SkuService.FindSkusAsync(db, item.Id, size.Id);
                Sku sku;
                if (skus.Count > 0)
                {
                    sku = skus[0];
                    //logger.LogInformation("SKU found for Order {OrderNumber} Gown: ItemId={ItemId}, SizeId={SizeId}, FitId={FitId}, SKU Id={SkuId}", orderDto.OrderNumber, item.Id, size.Id, fit.Id, skus[0].Id);
                }
                else
                {
                    logger.LogWarning("SKU not found for Order {OrderNumber} PhD Hood: ItemId={ItemId}, SizeId={SizeId}", orderDto.OrderNumber, item.Id, size.Id);
                    // Optionally, you could create a new SKU if it doesn't exist
                    sku = await SkuService.InsertSkuAsync(db, item.Id, size.Id);
                    logger.LogInformation("Inserted new SKU for Order {OrderNumber} PhD Hood: ItemId={ItemId}, SizeId={SizeId}", orderDto.OrderNumber, item.Id, size.Id);
                }
                return ToOrderedItem(item, sku);
            }
            else
            {
                //logger.LogWarning("Order {OrderNumber} Hood: Missing item or hood. ItemId={ItemId}, HoodId={HoodId}", orderDto.OrderNumber, item?.Id, hood?.Id);
            }
            return null;
        }

        private static async Task<OrderedItems?> ProcessStoleAsync(OrderDtoIn orderDto, GownDb db, ILogger logger)
        {

            if (orderDto.Stole is { Count: > 0 })
            {
                Items? item = null;
                Sizes? size = null;
                if (orderDto.Stole.TryGetValue("Name", out var name) && name != null)
                {
                    //logger.LogInformation("Order {OrderNumber} Stole Name={Name}", orderDto.OrderNumber, name);

                    var itemName = name?.Trim().ToLower();
                    item = string.IsNullOrEmpty(itemName)
                                                    ? null
                                                    : await db.items.FirstOrDefaultAsync(i => i.Name.ToLower() == itemName);

                    if (item is null)
                        logger.LogWarning("Order {OrderNumber} Stole item '{Name}' not found", orderDto.OrderNumber, name);
                    //else
                    //    logger.LogInformation("Order {OrderNumber} Stole item found: Id={ItemId}, Name={ItemName}", orderDto.OrderNumber, item.Id, item.Name);
                }

                if (orderDto.Stole.TryGetValue("Size", out var hatSize) && hatSize != null)
                {
                    var itemSize = hatSize?.Trim().ToLower();
                    size = string.IsNullOrEmpty(itemSize)
                        ? null
                        : await db.sizes.FirstOrDefaultAsync(s => s.Size.ToLower() == itemSize);
                    //logger.LogInformation("Order {OrderNumber} Stole Size={Size} Size Id={id}, Size Name={Name}", orderDto.OrderNumber, hatSize, size?.Id, size?.Size);
                }
                if (item != null && size != null)
                {
                    var skus = await SkuService.FindSkusAsync(db, item.Id, size.Id);
                    Sku sku;
                    if (skus.Count > 0)
                    {
                        sku = skus[0];
                        //logger.LogInformation("SKU found for Order {OrderNumber} Stole: ItemId={ItemId}, SizeId={SizeId}, SKU Id={SkuId}", orderDto.OrderNumber, item.Id, size.Id, skus[0].Id);
                    }
                    else
                    {
                        logger.LogWarning("SKU not found for Order {OrderNumber} Stole: ItemId={ItemId}, SizeId={SizeId}", orderDto.OrderNumber, item.Id, size.Id);
                        // Optionally, you could create a new SKU if it doesn't exist
                        sku = await SkuService.InsertSkuAsync(db, item.Id, size.Id, null, null, size.Id);
                        logger.LogInformation("Inserted new SKU for Order {OrderNumber} Stole: ItemId={ItemId}, SizeId={SizeId}", orderDto.OrderNumber, item.Id, size.Id);
                    }
                    return ToOrderedItem(item, sku);
                }
            }
            else
            {
                logger.LogInformation("Order {OrderNumber} Stole: <empty>", orderDto.OrderNumber);
            }
            return null;
        }

    }
}
