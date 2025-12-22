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
                    var lowered = nameQ.ToLowerInvariant();

                    if (int.TryParse(nameQ, out var id))
                    {
                        q = q.Where(o => o.Id == id);
                    }
                    else
                    {
                        q = q.Where(o =>
                            ((o.FirstName ?? "") + " " + (o.LastName ?? ""))
                                .ToLower()
                                .Contains(lowered));
                    }
                }

                var labels = await q
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new AddressLabelDto
                    {
                        LabelType = "individual",
                        OrderType = "individual",
                        SourceId = o.Id,
                        OrderDate = o.OrderDate,
                        OrderNumber = o.Id.ToString(),
                        ToName = (o.FirstName ?? "") + " " + (o.LastName ?? ""),
                        Attn = o.FirstName,
                        Phone = o.Mobile,
                        Address1 = o.Address ?? "",
                        Address2 = null,
                        City = o.City ?? "",
                        Postcode = o.Postcode ?? ""
                    })
                    .ToListAsync();

                return Ok(labels);
            }

            var q2 = _db.bulkOrders
                .AsNoTracking()
                .Join(
                    _db.ceremonies.AsNoTracking(),
                    bo => bo.CeremonyId,
                    c => c.Id,
                    (bo, c) => new { bo, c }
                )
                .AsQueryable();

            if (dateFrom.HasValue)
                q2 = q2.Where(x => x.c.DespatchDate.HasValue && x.c.DespatchDate.Value >= dateFrom.Value);

            if (dateTo.HasValue)
                q2 = q2.Where(x => x.c.DespatchDate.HasValue && x.c.DespatchDate.Value <= dateTo.Value);

            var instNameQ = (name ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(instNameQ))
            {
                var lowered = instNameQ.ToLowerInvariant();

                if (int.TryParse(instNameQ, out var id))
                {
                    q2 = q2.Where(x => x.bo.Id == id);
                }
                else
                {
                    q2 = q2.Where(x => (x.c.InstitutionName ?? "").ToLower().Contains(lowered));
                }
            }

            var labels2 = await q2
                .OrderByDescending(x => x.c.DespatchDate)
                .Select(x => new AddressLabelDto
                {
                    LabelType = "institution",
                    OrderType = "institution",
                    SourceId = x.bo.Id,
                    OrderDate = null,
                    OrderNumber = x.bo.Id.ToString(),
                    ToName = x.c.InstitutionName ?? "",
                    Attn = x.c.Organiser,
                    Phone = x.c.Phone,
                    Address1 = x.c.CourierAddress ?? "",
                    Address2 = null,
                    City = x.c.City ?? "",
                    Postcode = ""
                })
                .ToListAsync();

            return Ok(labels2);
        }
    }
}
