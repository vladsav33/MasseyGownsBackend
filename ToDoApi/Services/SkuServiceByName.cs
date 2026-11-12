using GownApi.Model;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Services
{
    public static class SkuServiceByName
    {
        /// <summary>
        /// Finds the SKU id for a hat, given the hat's name (items.name) and size (hats.size).
        /// Both are matched case-insensitively after trimming. Returns null if the item,
        /// the hat size, or the SKU can't be found.
        /// </summary>
        public static async Task<int?> FindHatSkuIdAsync(GownDb db, string? hatName, string? hatSize)
        {
            if (string.IsNullOrWhiteSpace(hatName) || string.IsNullOrWhiteSpace(hatSize))
                return null;

            var name = hatName.Trim().ToLower();
            var size = hatSize.Trim().ToLower();

            var itemId = await db.items
                .Where(i => i.Name.ToLower() == name)
                .Select(i => (int?)i.Id)
                .FirstOrDefaultAsync();
            if (itemId is null)
                return null;

            var hatId = await db.hats
                .Where(h => h.ItemId == itemId && h.Size.ToLower() == size)
                .Select(h => (int?)h.Id)
                .FirstOrDefaultAsync();
            if (hatId is null)
                return null;

            return await db.Sku
                .Where(s => s.ItemId == itemId && s.HatId == hatId)
                .OrderBy(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
        }
    }
}
