using Azure.Storage;
using Azure.Storage.Blobs;
using GownApi;
using GownApi.Endpoints;
using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//Serilog: read from appsettings.json (Serilog section)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Email settings
var emailSettings = builder.Configuration.GetSection("Email").Get<EmailSettings>();
if (emailSettings is not null)
{
    builder.Services.AddSingleton(emailSettings);
}
else
{
    // still run, but log a warning so it's visible
    Log.Warning("Email settings are missing or invalid in configuration section: Email");
}

// Email service
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPdfService, PdfService>();

var connectionString = builder.Configuration.GetConnectionString("GownDb");

var key = Encoding.ASCII.GetBytes("SuperSecretKey123!"); // Use a secure key in production

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

    // Fallback: use account name + key
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

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));

var app = builder.Build();

// Correlation ID middleware 
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(correlationId))
        correlationId = context.TraceIdentifier;

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await next();
    }
});

//OrderNo middleware: capture ORD from header/query/route and push into LogContext
// Put it after CorrelationId middleware so logs include both Corr + Order when available.
app.Use(async (context, next) =>
{
   
    var orderNo = context.Request.Headers["X-Order-No"].FirstOrDefault();

  
    if (string.IsNullOrWhiteSpace(orderNo))
        orderNo = context.Request.Query["orderNo"].FirstOrDefault();

 
    if (string.IsNullOrWhiteSpace(orderNo) &&
        context.Request.RouteValues.TryGetValue("orderNo", out var rv))
    {
        orderNo = rv?.ToString();
    }

    if (!string.IsNullOrWhiteSpace(orderNo))
    {
        using (Serilog.Context.LogContext.PushProperty("OrderNo", orderNo))
        {
            await next();
        }
        return;
    }

    await next();
});

//Request logging: method/path/status/elapsed + enrich extra fields
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        // CORS preflight 
        if (HttpMethods.IsOptions(httpContext.Request.Method))
            return LogEventLevel.Debug;

        if (ex != null)
            return LogEventLevel.Error;

        return LogEventLevel.Information;
    };

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.Response.Headers["X-Correlation-ID"].ToString());
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());

        string? orderNo = null;

        // Try to get OrderNo for request log line
        if (httpContext.Items.TryGetValue("OrderNo", out var ordObj)
            && ordObj is string ord
            && !string.IsNullOrWhiteSpace(ord))
        {
            orderNo = ord;
        }
        else
        {
            // Fallbacks (optional)
            var ordHeader = httpContext.Request.Headers["X-Order-No"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ordHeader))
            {
                orderNo = ordHeader;
            }
            else
            {
                var ordQuery = httpContext.Request.Query["orderNo"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(ordQuery))
                {
                    orderNo = ordQuery;
                }
            }
        }

        // Always set both, so template can decide whether to show the bracket part
        diagnosticContext.Set("OrderNo", orderNo ?? "");
        diagnosticContext.Set("OrderTag", string.IsNullOrWhiteSpace(orderNo) ? "" : $" (Order:{orderNo})");
    };
});

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowFrontend");

app.UseSwagger();
app.UseSwaggerUI();

app.AdminBulkOrderEndpoints();
app.AdminItemsEndpoints();
app.AdminDegreeEndpoints();
app.AdminCeremonyEndpoints();
app.AdminUserEndpoints();
app.AdminHoodEndpoints();
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
app.MapEmailEndpoints();

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
        // Optional: log server-side as well
        Log.Error(ex, "Blob connectivity test failed");
        return Results.Problem(ex.Message);
    }
});

try
{
    Log.Information("API starting up. Environment={Env}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}