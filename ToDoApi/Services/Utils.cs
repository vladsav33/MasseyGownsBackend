using GownApi.Dto;
using GownsApi;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata.Ecma335;

namespace GownApi.Services
{
    public interface IItemBase
    {
        int Id { get; }
        int? DegreeId { get; }
        string Name { get; }
        byte[]? Picture { get; }
        float? HirePrice { get; set; }
        float? BuyPrice { get; set; }
        string Category { get; }
        string? Description { get; }
        bool IsHiring { get; }
    }


    public static class Utils
    {
        public static async Task<ItemDto> GetOptions<T>(T items, GownDb db) where T : IItemBase
        {
            var sizes = await db.sizes
                .FromSqlRaw("SELECT s.size FROM sizes s WHERE s.item_id = {0} and (s.fit_id = 1 OR s.fit_id IS NULL)",
                    items.Id)
                .Select(s => s.Size)
                .ToListAsync();

            var fit = await db.fit
                .FromSqlRaw("SELECT DISTINCT f.fit_type FROM sizes s INNER JOIN fit f ON s.fit_id = f.id WHERE s.item_id = {0} order by f.fit_type",
                    items.Id)
                .Select(f => f.FitType)
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

            if (items.Category == "Academic Gown")
            {
                itemDto.Options = new List<Dictionary<string, object>>
                    {
                    new () {
                        ["label"] = "Gown Type",
                        ["value"] = fit[0],
                        ["choices"] = fit
},
                    new () {
                        ["label"] = "My full height",
                        ["value"] = sizes[0],
                        ["choices"] = sizes
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
            return itemDto;
        }

        public static async Task<ItemDto> GetSetOptions<T>(T items, GownDb db) where T : IItemBase
        {
            var sizes = await db.sizes
                .FromSqlRaw("SELECT s.size FROM sizes s WHERE s.item_id = {0} and s.fit_id = 1",
                    items.Id)
                .Select(s => s.Size)
                .ToListAsync();

            var headSizes = await db.sizes
                .FromSqlRaw("SELECT s.size FROM sizes s WHERE s.item_id = {0} and s.fit_id is null",
                    items.Id)
                .Select(s => s.Size)
                .ToListAsync();

            var fit = await db.fit
                .FromSqlRaw("SELECT DISTINCT f.fit_type FROM sizes s INNER JOIN fit f ON s.fit_id = f.id WHERE s.item_id = {0} order by f.fit_type",
                    items.Id)
                .Select(f => f.FitType)
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

            itemDto.Options = new List<Dictionary<string, object>>
                {
                    new () {
                        ["label"] = "Gown Type",
                        ["value"] = fit[0],
                        ["choices"] = fit
                    },
                    new () {
                        ["label"] = "My full height",
                        ["value"] = sizes[0],
                        ["choices"] = sizes
                    },
                    new ()
                    {
                        ["label"] = "Head Size",
                        ["value"] = headSizes[0],
                        ["choices"] = headSizes
                    }
            };
            return itemDto;
        }
    }
}
