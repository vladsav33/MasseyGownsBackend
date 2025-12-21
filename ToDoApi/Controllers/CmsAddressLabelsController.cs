using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GownApi.Model.Dto;

namespace GownApi.Controllers
{
    [ApiController]
    [Route("api/cms/address-labels")]
    public class CmsAddressLabelsController : ControllerBase
    {
        
        private readonly GownDb _db;

        public CmsAddressLabelsController(GownDb db)
        {
            _db = db;
        }

        /// <summary>
        /// type: "individual" | "institution"
        /// dateFrom/dateTo: yyyy-MM-dd
        /// name: search by full name (first + last) or institution name
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<AddressLabelDto>>> GetLabels(
            [FromQuery] string type,
            [FromQuery] DateOnly? dateFrom,
            [FromQuery] DateOnly? dateTo,
            [FromQuery] string? name)
        {
            type = (type ?? "").Trim().ToLowerInvariant();

            if (type != "individual" && type != "institution")
                return BadRequest("type must be 'individual' or 'institution'.");


            if (type == "individual")
            {
                var q = _db.orders.AsNoTracking().AsQueryable();

                if (dateFrom.HasValue)
                    q = q.Where(o => o.OrderDate >= dateFrom.Value);

                if (dateTo.HasValue)
                    q = q.Where(o => o.OrderDate <= dateTo.Value);

                var nameQ = (name ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(nameQ))
                {
                    var lowered = nameQ.ToLower();
                    q = q.Where(o =>
                        ((o.FirstName ?? "") + " " + (o.LastName ?? ""))
                            .ToLower()
                            .Contains(lowered));
                }

                var labels = await q
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new AddressLabelDto
                    {
                        LabelType = "individual",
                        SourceId = o.Id,
                        ToName = (o.FirstName ?? "") + " " + (o.LastName ?? ""),
                        Attn = o.FirstName,          // 你之前确认：个人 Attn = first_name
                        Phone = o.Mobile,
                        Address1 = o.Address ?? "",
                        Address2 = null,
                        City = o.City ?? "",
                        Postcode = o.Postcode ?? ""
                    })
                    .ToListAsync();

                return Ok(labels);
            }



            // institution
            // data: bulkOrders + ceremonies
            // join: bulkOrders.CeremonyId == ceremonies.Id
            var q2 = _db.bulkOrders
                .AsNoTracking()
                .Join(
                    _db.ceremonies.AsNoTracking(),
                    bo => bo.CeremonyId,   // 如果这里报错，说明 BulkOrder 实体字段名不是 CeremonyId（下一步我再帮你对齐）
                    c => c.Id,
                    (bo, c) => new { bo, c }
                )
                .AsQueryable();

            // date filter uses ceremonies.DespatchDate
            if (dateFrom.HasValue)
                q2 = q2.Where(x => x.c.DespatchDate.HasValue && x.c.DespatchDate.Value >= dateFrom.Value);

            if (dateTo.HasValue)
                q2 = q2.Where(x => x.c.DespatchDate.HasValue && x.c.DespatchDate.Value <= dateTo.Value);

            var instNameQ = (name ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(instNameQ))
            {
                var lowered = instNameQ.ToLower();
                q2 = q2.Where(x => (x.c.InstitutionName ?? "").ToLower().Contains(lowered));
            }

            var labels2 = await q2
                .OrderByDescending(x => x.c.DespatchDate)
                .Select(x => new AddressLabelDto
                {
                    LabelType = "institution",
                    SourceId = x.bo.Id,
                    ToName = x.c.InstitutionName ?? "",
                    Attn = x.c.Organiser,
                    Phone = x.c.Phone,
                    Address1 = x.c.CourierAddress ?? "",
                    Address2 = null,
                    City = x.c.City ?? "",
                    Postcode = "" // ceremonies 没有 Postcode 字段，所以先留空
                })
                .ToListAsync();

            return Ok(labels2);

        }
    }
}
