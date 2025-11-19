using Azure.Storage.Blobs;
using GownApi.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs.Models;
using System.IO;

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
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            // grab the record with Id = 1
            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            // If it didn't get the record, then return heroImageUrl = null，
            // Use default image
            var heroImageUrl = settings?.HeroImageUrl;

            return Ok(new
            {
                heroImageUrl
            });
        }

        [HttpPost("hero-image")]
        public async Task<IActionResult> UploadHeroImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            // 1.Get container name from configuration
            var containerName = _configuration.GetSection("BlobStorage")["HeroContainer"];
            if (string.IsNullOrEmpty(containerName))
            {
                return StatusCode(500, "Blob container name is not configured.");
            }

            // 2. Get and create container if not exists
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            await containerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

            // 3. Generate blob name，like hero-xxxx.png
            var extension = Path.GetExtension(file.FileName);
            var blobName = $"hero-{Guid.NewGuid()}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            // 4. Upload documents to Blob
            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, overwrite: true);
            }

            var heroImageUrl = blobClient.Uri.ToString();

            // 5. Update HomePageSettings（Id = 1）
            var settings = await _db.HomePageSettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            if (settings == null)
            {
                settings = new HomePageSettings
                {
                    Id = 1,
                    HeroImageUrl = heroImageUrl,
                    UpdateAt = DateTime.UtcNow
                };
                _db.HomePageSettings.Add(settings);
            }
            else
            {
                settings.HeroImageUrl = heroImageUrl;
                settings.UpdateAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                heroImageUrl
            });
        }



    }
}
