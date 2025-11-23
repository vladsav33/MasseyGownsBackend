using GownApi.Model.Dto;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Services
{
    public interface IItemBase
    {
        int Id { get; }
        //int? DegreeId { get; }
        string Name { get; }
        //string DegreeName { get; }
        byte[]? Picture { get; }
        float? HirePrice { get; set; }
        float? BuyPrice { get; set; }
        string Category { get; }
        string? Description { get; }
        bool IsHiring { get; }
    }

    public static class Utils
    {
        public static async Task<ItemDto> GetOptions(ItemDegreeModel items, GownDb db) // where T : IItemBase
        {
            var sizes = await db.sizes
                .FromSqlRaw("SELECT s.id, s.size, s.labelsize, s.price FROM sizes s INNER JOIN sku sk ON sk.size_id = s.id WHERE sk.item_id = {0} AND (sk.fit_id = 1 OR sk.fit_id IS NULL)",
                    items.Id)
                .Select(s => new { s.Id, Value = s.Size, Price = s.Price })
                .ToListAsync();

            var fit = await db.fit
                .FromSqlRaw("SELECT DISTINCT f.id, f.fit_type FROM fit f INNER JOIN sku sk ON sk.fit_id = f.id WHERE sk.item_id = {0} order by f.fit_type",
                    items.Id)
                .Select(f => new { f.Id, Value = f.FitType })
                .ToListAsync();

            var hoods = await db.hoods
                .FromSqlRaw("SELECT h.id, h.name FROM sku sk INNER JOIN hood_type h ON sk.hood_id = h.id WHERE sk.item_id = {0}",
                    items.Id)
                .Select(h => new { h.Id, Value = h.Name })
                .ToListAsync();

            var itemDto = new ItemDto
            {
                Id = items.Id,
                DegreeId = items.DegreeId,
                DegreeName = items.DegreeName,
                DegreeOrder = items.DegreeOrder,
                Name = items.Name,
                PictureBase64 = items.Picture != null ? Convert.ToBase64String(items.Picture) : null,
                HirePrice = items.HirePrice,
                BuyPrice = items.BuyPrice,
                Category = items.Category,
                Description = items.Description,
                IsHiring = items.IsHiring
            };

            if (items.Category == "Academic Gown")
            {
                itemDto.Options = new List<Dictionary<string, object>>
                {
                    new () {
                        ["label"] = "My full height",
                        ["value"] = sizes[0],
                        ["choices"] = sizes
                    },
                    new () {
                        ["label"] = "Gown Size",
                        ["value"] = fit[0],
                        ["choices"] = fit
                    }
                };
            }
            if (items.Category == "Headwear")
            {
                itemDto.Options = new List<Dictionary<string, object>> {
                                        new () {
                                            ["label"] = "Head Size",
                                            ["value"] = sizes[0],
                                            ["choices"] = sizes
                                        }
                                    };
            }
            if (items.Category == "Stole")
            {
                itemDto.Options = new List<Dictionary<string, object>> {
                                        new () {
                                            ["label"] = "My full height",
                                            ["value"] = sizes[0],
                                            ["choices"] = sizes
                                        }
                                    };
            }
            if (items.Category == "Hood")
            {
                itemDto.Options = new List<Dictionary<string, object>> {
                                        new () {
                                            ["label"] = "Hood Type",
                                            ["value"] = hoods[0],
                                            ["choices"] = hoods
                                        }
                                    };
            }
            if (items.Category == "Delivery")
            {
                itemDto.Options = new List<Dictionary<string, object>> {
                                        new () {
                                            ["label"] = "Delivery Type",
                                            ["value"] = sizes[0],
                                            ["choices"] = sizes
                                        }
                                    };
            }
            return itemDto;
        }

        public static async Task<ItemDto> GetSetOptions(ItemDegreeModel items, GownDb db) // where T : IItemBase
        {
            var sizes = await db.sizes
                //.FromSqlRaw("SELECT s.size FROM sizes s INNER JOIN sku sk ON sk.size_id = s.id WHERE sk.item_id = {0} AND (sk.fit_id = 1 OR sk.fit_id IS NULL)",
                //    items.Id)
                .FromSqlRaw("SELECT s.id, s.size FROM sizes s INNER JOIN items i ON i.id = s.item_id WHERE s.item_id = {0} AND s.fit_id = 1",
                    items.Id)
                .Select(s => new { s.Id, Value = s.Size })
                .ToListAsync();

            var headSizes = await db.sizes
                //.FromSqlRaw("SELECT s.size FROM sizes s INNER JOIN sku sk ON sk.size_id = s.id WHERE sk.item_id = {0} AND sk.fit_id IS NULL",
                //    items.Id)
                .FromSqlRaw("SELECT s.id, s.size FROM sizes s INNER JOIN items i ON i.id = s.item_id WHERE s.item_id = {0} AND s.fit_id IS NULL",
                    items.Id)
                .Select(s => new { s.Id, Value = s.Size })
                .ToListAsync();

            var hoods = await db.hoods
                .FromSqlRaw("SELECT h.id, h.name FROM items i INNER JOIN hood_type h ON h.item_id = i.id WHERE h.item_id = {0}",
                    items.Id)
                .Select(h => new { h.Id, Value = h.Name })
                .ToListAsync();

            var itemDto = new ItemDto
            {
                Id = items.Id,
                DegreeId = items.DegreeId,
                Name = items.Name,
                PictureBase64 = items.Picture != null ? Convert.ToBase64String(items.Picture) : null,
                HirePrice = items.HirePrice,
                BuyPrice = items.BuyPrice,
                Category = items.Category,
                Description = items.Description,
                IsHiring = items.IsHiring
            };
            var options = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["label"] = "My full height",
                    ["value"] = sizes[0],
                    ["choices"] = sizes
                },
                new()
                {
                    ["label"] = "Head Size",
                    ["value"] = headSizes[0],
                    ["choices"] = headSizes
                }
            };

            if (hoods?.Any() == true)
            {
                options.Add(new()
                {
                    ["label"] = "Hood Type",
                    ["value"] = hoods[0],
                    ["choices"] = hoods
                });
            }

            itemDto.Options = options;
            return itemDto;
        }
    }
}
