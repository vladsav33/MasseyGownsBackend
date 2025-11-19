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

var key = Encoding.ASCII.GetBytes("SuperSecretKey123!"); // 🔑 Use a secure key in production

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

//Register the DbContext to use SQL Server (Azure SQL)
builder.Services.AddDbContext<GownDb>(options =>
    options
           .UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddEndpointsApiExplorer(); // Required for minimal APIs
builder.Services.AddSwaggerGen();           // Adds Swagger generation

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddHttpClient();

builder.Services.Configure<PaystationOptions>(
    builder.Configuration.GetSection("Paystation"));//Joe 20251004 bind the Paystation section of appsettings.json to PaystationOptions class

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173",
                               "http://localhost:5174",                           
                               "https://masseygowns-bzhkgzfubkavgchw.newzealandnorth-01.azurewebsites.net")  // allow frontend to prevent CORS errors
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Joe 2025-11-19: register BlobServiceClient using env vars in Azure
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var accountName = config["StorageAccountName"];
    var accountKey = config["StorageAccountKey"];

    if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
    {
        throw new InvalidOperationException(
            "StorageAccountName or StorageAccountKey is not configured.");
    }

    var credential = new StorageSharedKeyCredential(accountName, accountKey);
    var blobUri = new Uri($"https://{accountName}.blob.core.windows.net");

    return new BlobServiceClient(blobUri, credential);
});


var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowFrontend");  // enable it

//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

app.MapDegreeEndpoints();
app.MapItemEndpoints();
app.MapFaqEndpoints();
app.MapCeremonyEndoints();
app.MapOrderEnpoints();
app.MapItemsetsEndpoints();
app.MapContactEndpoints();//Joe20250921
app.MapDeliveryEndpoints();
app.MapPaymentEndpoints();
app.MapControllers();


//app.MapGet("/todoitems/complete", async (GownDb db) =>
//    await db.degree.Where(t => t.IsComplete).ToListAsync());

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
