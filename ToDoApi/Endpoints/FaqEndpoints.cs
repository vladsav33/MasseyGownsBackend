using System.Linq;
using GownApi.Model;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Endpoints
{
    public static class FaqEndpoints
    {
        private const string FaqPageName = "FAQs";

        public static void MapFaqEndpoints(this WebApplication app)
        {
            // GET /faq
            app.MapGet("/faq", async (GownDb db) =>
            {
                // 先把所有 block 读出来
                var blocks = await db.CmsContentBlocks
                    .Where(b => b.Page == FaqPageName)
                    .ToListAsync();

                var items = blocks
                    .Select(b =>
                    {
                        var rawSection = b.Section ?? string.Empty;
                        var displaySection = rawSection;
                        var sectionOrder = 0;

                        // 解析类似 "Ordering and Payment.1" 的后缀
                        var lastDot = rawSection.LastIndexOf('.');
                        if (lastDot > 0 && lastDot < rawSection.Length - 1)
                        {
                            var maybeNumber = rawSection.Substring(lastDot + 1);
                            if (int.TryParse(maybeNumber, out var n))
                            {
                                sectionOrder = n;
                                displaySection = rawSection.Substring(0, lastDot);
                            }
                        }

                        return new
                        {
                            Block = b,
                            DisplaySection = displaySection,
                            SectionOrder = sectionOrder
                        };
                    })
                    .OrderBy(x => x.DisplaySection)   // 按 section 名排序
                    .ThenBy(x => x.SectionOrder)      // 再按你写在后缀的数字排序
                    .ThenBy(x => x.Block.Id)          // 最后用 id 稳定排序
                    .Select(x => new FaqItem
                    {
                        Id = x.Block.Id,
                        Page = x.Block.Page,
                        Section = x.DisplaySection,   // ✅ 前端看到的是“干净”的名字
                        Key = x.Block.Key,
                        Type = x.Block.Type,
                        Label = x.Block.Label,
                        Value = x.Block.Value
                    })
                    .ToList();

                return Results.Ok(items);
            });


            // POST /faq 
            app.MapPost("/faq", async (FaqItem item, GownDb db) =>
            {
                var section = item.Section?.Trim() ?? "";
                var label = item.Label?.Trim() ?? "";
                var type = string.IsNullOrWhiteSpace(item.Type) ? "text" : item.Type.Trim();
                var value = item.Value ?? "";

                var key = string.IsNullOrWhiteSpace(item.Key)
                    ? BuildKey(section, label)
                    : item.Key!.Trim();

                var block = new CmsContentBlock
                {
                    Page = FaqPageName,
                    Section = section,
                    Key = key,
                    Type = type,
                    Label = label,
                    Value = value
                };

                db.CmsContentBlocks.Add(block);
                await db.SaveChangesAsync();

               
                item.Id = block.Id;
                item.Page = block.Page;
                item.Key = block.Key;

                return Results.Created($"/faq/{item.Id}", item);
            });

            // PUT /faq/{id}  
            app.MapPut("/faq/{id}", async (int id, FaqItem item, GownDb db) =>
            {
                var block = await db.CmsContentBlocks
                    .FirstOrDefaultAsync(b => b.Id == id && b.Page == FaqPageName);

                if (block is null)
                    return Results.NotFound();

                var section = item.Section?.Trim() ?? "";
                var label = item.Label?.Trim() ?? "";
                var type = string.IsNullOrWhiteSpace(item.Type) ? "text" : item.Type.Trim();
                var value = item.Value ?? "";

                block.Section = section;
                block.Label = label;
                block.Type = type;
                block.Value = value;

                block.Key = string.IsNullOrWhiteSpace(item.Key)
                    ? BuildKey(section, label)
                    : item.Key!.Trim();

                await db.SaveChangesAsync();

                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

            // DELETE /faq/{id} 
            app.MapDelete("/faq/{id}", async (int id, GownDb db) =>
            {
                var block = await db.CmsContentBlocks
                    .FirstOrDefaultAsync(b => b.Id == id && b.Page == FaqPageName);

                if (block is null)
                    return Results.NotFound();

                db.CmsContentBlocks.Remove(block);
                await db.SaveChangesAsync();

                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
        }

      
        private static string BuildKey(string section, string label)
        {
            if (string.IsNullOrWhiteSpace(section) && string.IsNullOrWhiteSpace(label))
                return $"{FaqPageName}.item";

            if (string.IsNullOrWhiteSpace(section))
                return $"{FaqPageName}.{label}";

            if (string.IsNullOrWhiteSpace(label))
                return $"{section}.item";

            return $"{section}.{label}";
        }
    }

    
    public class FaqItem
    {
        public int Id { get; set; }
        public string? Page { get; set; }    
        public string? Section { get; set; }
        public string? Key { get; set; }
        public string? Type { get; set; }   
        public string? Label { get; set; }
        public string? Value { get; set; }
    }
}
