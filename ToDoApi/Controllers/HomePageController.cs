using System;
using System.Linq;
using System.Threading.Tasks;
using GownApi.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text.Json;
using ClosedXML.Excel;

namespace GownApi
{
    [ApiController]
    [Route("api/[controller]")] // => /api/CmsContent
    public class CmsContentController : ControllerBase
    {
        private readonly GownDb _db;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _config;

        public CmsContentController(
            GownDb db,
            BlobServiceClient blobServiceClient,
            IConfiguration config
        )
        {
            _db = db;
            _blobServiceClient = blobServiceClient;
            _config = config;
        }

        // Small helper so every API response has the same shape
        private IActionResult ApiResponse(
            bool success,
            string? message = null,
            object? data = null,
            int statusCode = 200)
        {
            var body = new
            {
                success,
                message,
                data
            };

            return statusCode switch
            {
                200 => Ok(body),
                400 => BadRequest(body),
                404 => NotFound(body),
                _ => StatusCode(statusCode, body)
            };
        }

        // ================== Get all CMS blocks  ==================

        /// <summary>
        /// Get all CMS content blocks.
        /// GET /api/CmsContent
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var blocks = await _db.CmsContentBlocks
                .OrderBy(x => x.Page)
                .ThenBy(x => x.Section)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return ApiResponse(true, "CMS blocks loaded.", blocks, 200);
        }

        // ================== DTOs ==================

        public class UpdateTextDto
        {
            public string Key { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        public class UpdateImageUrlDto
        {
            public string Key { get; set; } = string.Empty;
            public string ImageUrl { get; set; } = string.Empty;
        }

        public class UploadImageForm
        {
            public string Key { get; set; } = string.Empty;
            public IFormFile File { get; set; } = default!;
        }

        public class UpdateLinkDto
        {
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
        }

        public class UpdateListDto
        {
            public string Key { get; set; } = string.Empty;
            public string List { get; set; } = string.Empty;
        }

        public class UploadFileForm
        {
            public string Key { get; set; } = string.Empty;
            public IFormFile File { get; set; } = default!;
        }

        // ================== Save text block ==================

        /// <summary>
        /// Save a text content block.
        /// POST /api/CmsContent/save-text
        /// Body: { key, text }
        /// </summary>
        [HttpPost("save-text")]
        public async Task<IActionResult> SaveText([FromBody] UpdateTextDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Key))
            {
                return ApiResponse(false, "Key is required.", statusCode: 400);
            }

            var block = await _db.CmsContentBlocks
                .FirstOrDefaultAsync(b => b.Key == dto.Key && b.Type == "text");

            if (block == null)
            {
                return ApiResponse(false, $"Text block not found for key '{dto.Key}'.", statusCode: 404);
            }

            block.Value = dto.Text ?? string.Empty;
            block.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return ApiResponse(true, "Text updated successfully.", block, 200);
        }

        // ================== Save text block ==================

        /// <summary>
        /// Get a text content block.
        /// GET /api/CmsContent/get-text?key=home.hero.title
        /// </summary>
        [HttpGet("get-text")]
        public async Task<IActionResult> GetText([FromQuery] string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return ApiResponse(false, "Key is required.", statusCode: 400);
            }

            var block = await _db.CmsContentBlocks
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Key == key && b.Type == "text");

            if (block == null)
            {
                return ApiResponse(false, $"Text block not found for key '{key}'.", statusCode: 404);
            }

            return ApiResponse(true, "Text retrieved successfully.", new
            {
                key = block.Key,
                text = block.Value,
                updatedAt = block.UpdatedAt
            }, 200);
        }


        // ================== Upload Image ==================

        [HttpPost("upload-image")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> UploadImage([FromForm] UploadImageForm form)
        {
            if (string.IsNullOrWhiteSpace(form.Key))
            {
                return ApiResponse(false, "Key is required.", statusCode: 400);
            }

            if (form.File == null || form.File.Length == 0)
            {
                return ApiResponse(false, "Image file is required.", statusCode: 400);
            }

            var block = await _db.CmsContentBlocks
                .FirstOrDefaultAsync(b => b.Key == form.Key && b.Type == "image");

            if (block == null)
            {
                return ApiResponse(false, $"Image block not found for key '{form.Key}'.", statusCode: 404);
            }

            var containerName = _config["BlobStorage:HeroContainer"] ?? "site-assets";
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            var ext = Path.GetExtension(form.File.FileName);
            var safeExt = string.IsNullOrWhiteSpace(ext) ? ".png" : ext.ToLower();
            var newBlobName = $"{form.Key}-{Guid.NewGuid()}{safeExt}";
            var newBlobClient = containerClient.GetBlobClient(newBlobName);

            await using (var stream = form.File.OpenReadStream())
            {
                await newBlobClient.UploadAsync(stream, overwrite: true);

                await newBlobClient.SetHttpHeadersAsync(new BlobHttpHeaders
                {
                    ContentType = "image/png"
                });
            }

            var newImageUrl = newBlobClient.Uri.ToString();

            // Try to clean up the old image, but don't be dramatic if it fails
            if (!string.IsNullOrEmpty(block.Value))
            {
                try
                {
                    var oldUri = new Uri(block.Value);
                    var oldBlobName = Path.GetFileName(oldUri.AbsolutePath);
                    var oldBlobClient = containerClient.GetBlobClient(oldBlobName);
                    await oldBlobClient.DeleteIfExistsAsync();
                }
                catch
                {
                    // Cleanup is nice-to-have; we don't want it to break the upload
                }
            }

            block.Value = newImageUrl;
            block.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return ApiResponse(true, "Image uploaded and URL updated successfully.", new
            {
                key = block.Key,
                url = newImageUrl,
                block
            }, 200);
        }

        // ================== Save link block ==================

        /// <summary>
        /// Save a link content block.
        /// POST /api/CmsContent/save-link
        /// Body: { key, name, url }
        /// </summary>
        [HttpPost("save-link")]
        public async Task<IActionResult> SaveLink([FromBody] UpdateLinkDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Key))
            {
                return ApiResponse(false, "Key is required.", statusCode: 400);
            }

            var block = await _db.CmsContentBlocks
                .FirstOrDefaultAsync(b => b.Key == dto.Key && b.Type == "link");

            if (block == null)
            {
                return ApiResponse(false, $"Link block not found for key '{dto.Key}'.", statusCode: 404);
            }

            var payload = new
            {
                name = dto.Name ?? string.Empty,
                url = dto.Url ?? string.Empty
            };

            block.Value = JsonSerializer.Serialize(payload);
            block.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return ApiResponse(true, "Link updated successfully.", block, 200);
        }

        // ================== Save list block ==================

        /// <summary>
        /// Save a list content block.
        /// POST /api/CmsContent/save-list
        /// Body: { key, list }
        /// </summary>
        [HttpPost("save-list")]
        public async Task<IActionResult> SaveList([FromBody] UpdateListDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Key))
            {
                return ApiResponse(false, "Key is required.", statusCode: 400);
            }

            var block = await _db.CmsContentBlocks
                .FirstOrDefaultAsync(b => b.Key == dto.Key && b.Type == "list");

            if (block == null)
            {
                return ApiResponse(false, $"List block not found for key '{dto.Key}'.", statusCode: 404);
            }

            block.Value = dto.List ?? string.Empty;
            block.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return ApiResponse(true, "List updated successfully.", block, 200);
        }

        // ============ Helper: turn the first worksheet into { columns, rows } JSON ============

        /// <summary>
        /// Reads the first worksheet from an Excel stream and turns it into a simple
        /// { columns, rows } JSON payload. If anything goes wrong, we quietly return null
        /// so the file upload itself can still succeed.
        /// </summary>
        private static string? BuildTableJsonFromExcel(Stream stream)
        {
            try
            {
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null)
                    return null;

                var used = ws.RangeUsed();
                if (used == null)
                    return null;

                var rows = used.RowsUsed().ToList();
                if (rows.Count == 0)
                    return null;

                // First row = header
                var headerRow = rows[0];
                var columns = headerRow
                    .Cells()
                    // Use the formatted text, so things like "$48.00" stay exactly that
                    .Select(c => c.GetFormattedString())
                    .ToList();

                if (columns.Count == 0)
                    return null;

                // Remaining rows = data
                var dataRows = rows
                    .Skip(1)
                    .Select(r => r
                        .Cells()
                        // Same here: formatted text, not raw numbers
                        .Select(c => c.GetFormattedString())
                        .ToList())
                    // Skip rows that are completely empty visually
                    .Where(r => r.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                    .ToList();

                var tableObj = new
                {
                    columns,
                    rows = dataRows
                };

                return JsonSerializer.Serialize(tableObj);
            }
            catch
            {
                // If Excel parsing blows up, we just pretend there is no table data
                return null;
            }
        }


        // ================== Upload File ==================

        [HttpPost("upload-file")]
        [RequestSizeLimit(20_000_000)] // 20MB
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFile([FromForm] UploadFileForm form)
        {
            if (string.IsNullOrWhiteSpace(form.Key))
            {
                return ApiResponse(false, "Key is required.", statusCode: 400);
            }

            if (form.File == null || form.File.Length == 0)
            {
                return ApiResponse(false, "File is required.", statusCode: 400);
            }

            var block = await _db.CmsContentBlocks
                .FirstOrDefaultAsync(b => b.Key == form.Key && b.Type == "file");

            if (block == null)
            {
                return ApiResponse(false, $"File block not found for key '{form.Key}'.", statusCode: 404);
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient("site-assets");
            await containerClient.CreateIfNotExistsAsync();

            var ext = Path.GetExtension(form.File.FileName);
            var safeExt = string.IsNullOrWhiteSpace(ext) ? "" : ext.ToLowerInvariant();
            var newBlobName = $"{form.Key}-{Guid.NewGuid()}{safeExt}";
            var newBlobClient = containerClient.GetBlobClient(newBlobName);

            var incomingContentType = string.IsNullOrWhiteSpace(form.File.ContentType)
                ? "application/octet-stream"
                : form.File.ContentType;

            string? tableJson = null;
            var newFileUrl = string.Empty;

            await using (var ms = new MemoryStream())
            {
                // We buffer the upload so we can both send it to Blob Storage
                // and rewind it for Excel parsing.
                await form.File.CopyToAsync(ms);
                ms.Position = 0;

                // 1) push the file into Blob Storage
                await newBlobClient.UploadAsync(ms, overwrite: true);
                await newBlobClient.SetHttpHeadersAsync(new BlobHttpHeaders
                {
                    ContentType = incomingContentType
                });

                newFileUrl = newBlobClient.Uri.ToString();

                // 2) if the key ends with ".table", we try to treat it as a table source
                if (form.Key.EndsWith(".table", StringComparison.OrdinalIgnoreCase))
                {
                    ms.Position = 0; // rewind before reading as Excel
                    tableJson = BuildTableJsonFromExcel(ms);
                }
            }

            // Try to delete the previous blob for this block, if there was one
            if (!string.IsNullOrEmpty(block.Value))
            {
                try
                {
                    var oldUri = new Uri(block.Value);
                    var oldBlobName = Path.GetFileName(oldUri.AbsolutePath);
                    var oldBlobClient = containerClient.GetBlobClient(oldBlobName);
                    await oldBlobClient.DeleteIfExistsAsync();
                }
                catch
                {
                    // Again, cleanup is best-effort only
                }
            }

            // Update the file block itself
            block.Value = newFileUrl;
            block.UpdatedAt = DateTime.UtcNow;

            // If we managed to build table JSON, keep a separate "tableData" record alongside the file
            if (!string.IsNullOrWhiteSpace(tableJson))
            {
                // Example:
                // form.Key = "Ordering and Payment.Cost to Hire.table"
                // tableKey = "Ordering and Payment.Cost to Hire.tableData"
                var tableKey = form.Key + "Data";

                // Look up by Key only – key is unique, so this keeps us away from constraint errors
                var tableBlock = await _db.CmsContentBlocks
                    .FirstOrDefaultAsync(b => b.Key == tableKey);

                if (tableBlock == null)
                {
                    tableBlock = new CmsContentBlock
                    {
                        Page = block.Page,
                        Section = block.Section,
                        Key = tableKey,
                        Type = "table",
                        Label = string.IsNullOrWhiteSpace(block.Label)
                            ? "Table"
                            : $"{block.Label} table"
                    };
                    _db.CmsContentBlocks.Add(tableBlock);
                }
                else
                {
                    // Keep things in sync if the section or page ever change
                    tableBlock.Page = block.Page;
                    tableBlock.Section = block.Section;
                    tableBlock.Type = "table";
                    if (string.IsNullOrWhiteSpace(tableBlock.Label))
                    {
                        tableBlock.Label = string.IsNullOrWhiteSpace(block.Label)
                            ? "Table"
                            : $"{block.Label} table";
                    }
                }

                tableBlock.Value = tableJson;
                tableBlock.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return ApiResponse(true, "File uploaded successfully.", new
            {
                key = block.Key,
                url = newFileUrl,
                block
            }, 200);
        }
    }
}
