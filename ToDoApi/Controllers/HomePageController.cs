using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GownApi.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GownApi
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomePageController : ControllerBase
    {
        private readonly GownDb _db;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;

        public HomePageController(
            GownDb db,
            BlobServiceClient blobServiceClient,
            IConfiguration configuration)
        {
            _db = db;
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
        }

        /// <summary>
        /// Get current home page settings (hero image URL).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            var heroImageUrl = settings?.HeroImageUrl;

            return Ok(new
            {
                heroImageUrl
            });
        }

        /// <summary>
        /// Upload a new hero image, store it in Blob Storage,
        /// update the database, and delete the previous hero image if it exists.
        /// </summary>
        [HttpPost("hero-image")]
        // [Authorize] // temporarily disabled for testing from Swagger
        public async Task<IActionResult> UploadHeroImage(IFormFile file)
        {
            // Local helper to standardize API responses.
            IActionResult ApiResponse(bool success, string? message = null, object? data = null, int statusCode = 200)
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
                    500 => StatusCode(500, body),
                    _ => StatusCode(statusCode, body)
                };
            }

            // 1. Basic file checks
            if (file == null || file.Length == 0)
            {
                return ApiResponse(false, "No file uploaded.", statusCode: 400);
            }

            // 2. Max file size limit (5 MB)
            long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return ApiResponse(false, "File size cannot exceed 5 MB.", statusCode: 400);
            }

            // 3. Restrict allowed MIME types
            var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedContentTypes.Contains(file.ContentType))
            {
                return ApiResponse(false, "Only JPG, PNG, and WEBP images are allowed.", statusCode: 400);
            }

            // 4. Restrict allowed file extensions
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) ||
                (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".webp"))
            {
                return ApiResponse(false, "Invalid file extension.", statusCode: 400);
            }

            // 5. Resolve container name from configuration
            var containerName = _configuration.GetSection("BlobStorage")["HeroContainer"];
            if (string.IsNullOrEmpty(containerName))
            {
                return ApiResponse(false, "Blob container name is not configured.", statusCode: 500);
            }

            // 6. Get container client and ensure it exists and is public for blob read
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            await containerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

            // 7. Generate a unique blob name for the new hero image
            var blobName = $"hero-{Guid.NewGuid()}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            // 8. Set correct Content-Type so the image can be rendered in the browser
            var headers = new BlobHttpHeaders
            {
                ContentType = file.ContentType
            };

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = headers
            };

            // 9. Upload the image to Blob Storage with headers
            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, uploadOptions);
            }

            var newHeroImageUrl = blobClient.Uri.ToString();

            // 10. Load existing home page settings (Id = 1)
            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            string? oldHeroImageUrl = settings?.HeroImageUrl;

            // 11. Update or insert the HomePageSettings record
            if (settings == null)
            {
                settings = new HomePageSettings
                {
                    Id = 1,
                    HeroImageUrl = newHeroImageUrl,
                    UpdateAt = DateTime.UtcNow
                };
                _db.HomePageSettings.Add(settings);
            }
            else
            {
                settings.HeroImageUrl = newHeroImageUrl;
                settings.UpdateAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // 12. Delete old hero image blob if it exists
            if (!string.IsNullOrEmpty(oldHeroImageUrl))
            {
                try
                {
                    var oldUri = new Uri(oldHeroImageUrl);
                    var oldBlobName = Path.GetFileName(oldUri.AbsolutePath);
                    var oldBlobClient = containerClient.GetBlobClient(oldBlobName);
                    await oldBlobClient.DeleteIfExistsAsync();
                }
                catch
                {
                    // Swallow errors from deleting old blobs to avoid breaking the main operation.
                }
            }

            // 13. Return standardized response
            return ApiResponse(
                success: true,
                message: "Hero image updated successfully.",
                data: new
                {
                    heroImageUrl = newHeroImageUrl
                },
                statusCode: 200
            );
        }
    }
}
