using GownApi;
using GownApi.Endpoints;
using GownApi.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Azure.Storage;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("GownDb");

var key = Encoding.ASCII.GetBytes("SuperSecretKey123!"); //  Use a secure key in production

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// Register the DbContext to use PostgreSQL
builder.Services.AddDbContext<GownDb>(options =>
    options
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddHttpClient();

builder.Services.Configure<PaystationOptions>(
    builder.Configuration.GetSection("Paystation"));

// CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "http://localhost:5174",
                    "https://masseygowns-bzhkgzfubkavgchw.newzealandnorth-01.azurewebsites.net"
                )
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// BlobServiceClient registration
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    // Preferred: use connection string (works both locally and in Azure if configured)
    var blobConnectionString = config["BlobStorage:ConnectionString"];
    if (!string.IsNullOrEmpty(blobConnectionString))
    {
        return new BlobServiceClient(blobConnectionString);
    }

    // Fallback: use account name + key (your original approach)
    var accountName = config["StorageAccountName"];
    var accountKey = config["StorageAccountKey"];

    if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
    {
        throw new InvalidOperationException(
            "Blob storage is not configured. Set 'BlobStorage:ConnectionString' or 'StorageAccountName' and 'StorageAccountKey'.");
    }

    var credential = new StorageSharedKeyCredential(accountName, accountKey);
    var blobUri = new Uri($"https://{accountName}.blob.core.windows.net");

    return new BlobServiceClient(blobUri, credential);
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowFrontend");

app.UseSwagger();
app.UseSwaggerUI();

app.MapDegreeEndpoints();
app.MapItemEndpoints();
app.MapFaqEndpoints();
app.MapCeremonyEndoints();
app.MapOrderEnpoints();
app.MapItemsetsEndpoints();
app.MapContactEndpoints();
app.MapDeliveryEndpoints();
app.MapPaymentEndpoints();
app.MapControllers();

// Simple test endpoint for Blob connectivity
app.MapGet("/test-blob", async (BlobServiceClient blobClient) =>
{
    try
    {
        var containers = blobClient.GetBlobContainersAsync();
        var list = new List<string>();

        await foreach (var container in containers)
        {
            list.Add(container.Name);
        }

        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();
