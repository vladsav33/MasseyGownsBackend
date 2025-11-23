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

        // Simple standardized response wrapper
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
            /// <summary>
            /// CMS key, e.g. "home.heroImage"
            /// </summary>
            public string Key { get; set; } = string.Empty;

            /// <summary>
            /// Final image URL (e.g. Azure Blob URL)
            /// </summary>
            public string ImageUrl { get; set; } = string.Empty;
        }

        public class UploadImageForm
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

            // Create new blob name
            var ext = Path.GetExtension(form.File.FileName);
            var safeExt = string.IsNullOrWhiteSpace(ext) ? ".png" : ext.ToLower();
            var newBlobName = $"{form.Key}-{Guid.NewGuid()}{safeExt}";
            var newBlobClient = containerClient.GetBlobClient(newBlobName);

            // upload new file
            await using (var stream = form.File.OpenReadStream())
            {
                await newBlobClient.UploadAsync(stream, overwrite: true);

                // set content-type
                await newBlobClient.SetHttpHeadersAsync(new BlobHttpHeaders
                {
                    ContentType = "image/png"
                });
            }

            var newImageUrl = newBlobClient.Uri.ToString();

            // ======== delete previous file ========
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

                }
            }

            // update database
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

    }
}
