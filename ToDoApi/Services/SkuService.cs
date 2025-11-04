using GownApi.Model;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Services
{
    public static class SkuService
    {
        public static async Task<List<Sku>> FindSkusAsync(
            GownDb db,
            int itemId,
            int? sizeId = null,
            int? fitId = null,
            int? hoodId = null )
        {
            var query = db.Sku.AsQueryable();

            query = query.Where(x => x.ItemId == itemId);

            if (sizeId.HasValue && sizeId != 0)
                query = query.Where(x => x.SizeId == sizeId);

            if (fitId.HasValue && fitId != 0)
                query = query.Where(x => x.FitId == fitId);

            if (hoodId.HasValue && hoodId != 0)
                query = query.Where(x => x.HoodId == hoodId);

            return await query.ToListAsync();
        }

    }
}
