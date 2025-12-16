using GownApi.Model;
using GownApi.Model.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailTemplatesController : ControllerBase
    {
        private readonly GownDb _db;

        public EmailTemplatesController(GownDb db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var templates = await _db.EmailTemplates
                .OrderBy(t => t.Name)
                .ToListAsync();

            return Ok(templates);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] EmailTemplateUpdateDto dto)
        {
            var tpl = await _db.EmailTemplates.FindAsync(id);
            if (tpl == null) return NotFound();

            tpl.SubjectTemplate = dto.SubjectTemplate;
            tpl.BodyHtml = dto.BodyHtml;
            tpl.TaxReceiptHtml = dto.TaxReceiptHtml;
            tpl.CollectionDetailsHtml = dto.CollectionDetailsHtml ?? "";

            await _db.SaveChangesAsync();
            return Ok(tpl);
        }

        [HttpGet("by-name/{name}")]
        public async Task<IActionResult> GetByName(string name)
        {
            var template = await _db.EmailTemplates
                .SingleOrDefaultAsync(t => t.Name == name);

            if (template == null) return NotFound();
            return Ok(template);
        }
    }
}
