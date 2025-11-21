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
    public class UpdateCeremonyTextRequest
    {
        public string? Text { get; set; }
    }
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
        /// Standardized API response wrapper.
        /// </summary>
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
                500 => StatusCode(500, body),
                _ => StatusCode(statusCode, body)
            };
        }

        /// <summary>
        /// Get current home page settings (simple shape).
        /// GET /api/HomePage
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
        /// Get current hero image URL with standardized response.
        /// GET /api/HomePage/hero-image
        /// </summary>
        [HttpGet("hero-image")]
        public async Task<IActionResult> GetHeroImage()
        {
            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            var heroImageUrl = settings?.HeroImageUrl;

            return ApiResponse(
                success: true,
                message: "Hero image loaded successfully.",
                data: new
                {
                    heroImageUrl
                },
                statusCode: 200
            );
        }

        /// Get current ceremony image URL
        [HttpGet("ceremony-image")]
        public async Task<IActionResult> GetCeremonyImage()
        {
            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            var ceremonyImageUrl = settings?.CeremonyImageUrl;

            return ApiResponse(
                success: true,
                message: "Ceremony image loaded successfully.",
                data: new
                {
                    ceremonyImageUrl
                },
                statusCode: 200
            );
        }

        /// Get ceremony text
        [HttpGet("ceremony-text")]
        public async Task<IActionResult> GetCeremonyText()
        {
            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            return ApiResponse(
                success: true,
                message: "Ceremony text loaded successfully.",
                data: new
                {
                    ceremonyText = settings?.CeremonyText
                },
                statusCode: 200
            );
        }


        /// <summary>
        /// Upload a new hero image, store it in Blob Storage,
        /// update the database, and delete the previous hero image if it exists.
        /// POST /api/HomePage/hero-image
        /// </summary>
        [HttpPost("hero-image")]
        // [Authorize] // enable this when auth is ready
        public async Task<IActionResult> UploadHeroImage(IFormFile file)
        {
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
                    // Ignore failures when deleting old blobs.
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

        /// Upload a new ceremony image
        [HttpPost("ceremony-image")]
        public async Task<IActionResult> UploadCeremonyImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return ApiResponse(false, "No file uploaded.", statusCode: 400);
            }

            long maxFileSize = 5 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return ApiResponse(false, "File size cannot exceed 5 MB.", statusCode: 400);
            }

            var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedContentTypes.Contains(file.ContentType))
            {
                return ApiResponse(false, "Only JPG, PNG, and WEBP images are allowed.", statusCode: 400);
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) ||
                (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".webp"))
            {
                return ApiResponse(false, "Invalid file extension.", statusCode: 400);
            }

            var containerName = _configuration.GetSection("BlobStorage")["HeroContainer"];
            if (string.IsNullOrEmpty(containerName))
            {
                return ApiResponse(false, "Blob container name is not configured.", statusCode: 500);
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            await containerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

            var blobName = $"ceremony-{Guid.NewGuid()}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders
            {
                ContentType = file.ContentType
            };

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = headers
            };

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, uploadOptions);
            }

            var newCeremonyImageUrl = blobClient.Uri.ToString();

            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            var oldCeremonyImageUrl = settings?.CeremonyImageUrl;

            if (settings == null)
            {
                settings = new HomePageSettings
                {
                    Id = 1,
                    CeremonyImageUrl = newCeremonyImageUrl,
                    UpdateAt = DateTime.UtcNow
                };
                _db.HomePageSettings.Add(settings);
            }
            else
            {
                settings.CeremonyImageUrl = newCeremonyImageUrl;
                settings.UpdateAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(oldCeremonyImageUrl))
            {
                try
                {
                    var oldUri = new Uri(oldCeremonyImageUrl);
                    var oldBlobName = Path.GetFileName(oldUri.AbsolutePath);
                    var oldBlobClient = containerClient.GetBlobClient(oldBlobName);
                    await oldBlobClient.DeleteIfExistsAsync();
                }
                catch
                {
                }
            }

            return ApiResponse(
                success: true,
                message: "Ceremony image updated successfully.",
                data: new
                {
                    ceremonyImageUrl = newCeremonyImageUrl
                },
                statusCode: 200
            );
        }

        /// Upload a new ceremony text

        [HttpPost("ceremony-text")]
        public async Task<IActionResult> UpdateCeremonyText([FromBody] UpdateCeremonyTextRequest request)
        {
            var newText = request?.Text ?? string.Empty;

            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            if (settings == null)
            {
                settings = new HomePageSettings
                {
                    Id = 1,
                    CeremonyText = newText,
                    UpdateAt = DateTime.UtcNow
                };
                _db.HomePageSettings.Add(settings);
            }
            else
            {
                settings.CeremonyText = newText;
                settings.UpdateAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return ApiResponse(
                success: true,
                message: "Ceremony text updated successfully.",
                data: new
                {
                    ceremonyText = newText
                },
                statusCode: 200
            );
        }


    }
}
