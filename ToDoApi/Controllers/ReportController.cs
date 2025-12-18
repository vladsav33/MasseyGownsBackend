using Microsoft.AspNetCore.Mvc;
using GownApi.Model.Dto;

namespace GownApi.Controllers
{
    [ApiController]
    [Route("api/pdf")]
    public class PdfController : Controller
    {

        private readonly IPdfService _pdf;

        public PdfController(IPdfService pdf)
        {
            _pdf = pdf;
        }

        [HttpPost("print-pdf")]
        public async Task<IActionResult> PrintPdf([FromBody] HtmlDto dto)
        {
            var pdf = await _pdf.HtmlToPdfAsync(dto.Html);
            return File(pdf, "application/pdf", "report.pdf");
        }
    }
}
