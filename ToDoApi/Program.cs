using Azure.Storage;
using Azure.Storage.Blobs;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using GownApi;
using GownApi.Endpoints;
using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
using GownApi.Services.Paystation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System.Linq;
using System.Security.Claims;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

//Serilog: read from appsettings.json (Serilog section)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Email settings
//var emailSettings = builder.Configuration.GetSection("Email").Get<EmailSettings>();
/*if (emailSettings is not null)
{
    builder.Services.AddSingleton(emailSettings);
}
else
{
    // still run, but log a warning so it's visible
    Log.Warning("Email settings are missing or invalid in configuration section: Email");
}*/

// Email service

builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IQueueJobPublisher, QueueJobPublisher>();


var connectionString = builder.Configuration.GetConnectionString("GownDb");

var key = Encoding.ASCII.GetBytes("iREWTEWGfgweGERWgtGWgwET$#%q34GG#$%%3$##%GHBNBsgfdgwe345");


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,

         //JWT
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
});

// Register the DbContext to use PostgreSQL
builder.Services.AddDbContext<GownDb>(options =>
    options
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(); JWT
//JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GownApi", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<PaystationPayMeService>();
builder.Services.AddScoped<PaymentReminderJob>();
builder.Services.AddHttpClient<PaystationQuickLookupClient>();
builder.Services.AddHttpClient("Paystation", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.Configure<PaystationOptions>(
    builder.Configuration.GetSection("Paystation"));

var gatewayId = builder.Configuration["Paystation:GatewayId"];
var isPaystationonDev = string.Equals(gatewayId, "DEVELOPMENT", StringComparison.Ordinal);

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

//builder.Services.Configure<SmtpSettings>(
//    builder.Configuration.GetSection("Smtp"));

var app = builder.Build();

app.UseExceptionHandler("/error");

// MerchantReference & Correlation ID middleware 
app.Use(async (context, next) =>
{
    var correlationId = context.TraceIdentifier;

    context.Response.Headers["CorrelationID"] = correlationId;

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
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
        diagnosticContext.Set("CorrelationId", httpContext.Response.Headers["CorrelationID"].ToString());
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("MerchantReference", httpContext.Items["MerchantReference"]?.ToString() ?? "NotCreatedYet");
    };
});

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

if (isPaystationonDev) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Map("/error", (HttpContext httpContext, ILogger<Program> logger) =>
{
    var exceptionFeature = httpContext.Features.Get<IExceptionHandlerPathFeature>();
    var ex = exceptionFeature?.Error;

    var traceId = httpContext.Response.Headers["CorrelationID"].FirstOrDefault()
                  ?? httpContext.TraceIdentifier;

    var originalPath = exceptionFeature?.Path ?? httpContext.Request.Path.ToString();

    var MerchantReference = httpContext.Items["MerchantReference"]?.ToString() ?? "NotCreatedYet";

    if (ex is not null)
    {
        logger.LogError(
            ex,
            "Unhandled exception. Path={Path}, Method={Method}, TraceId={TraceId}, MerchantReference={MerchantReference}",
            originalPath,
            httpContext.Request.Method,
            traceId,
            MerchantReference);
    }
    else
    {
        logger.LogError(
            "Unhandled exception reached /error, but no exception details were available. Path={Path}, Method={Method}, TraceId={TraceId},MerchantReference={MerchantReference} ",
            originalPath,
            httpContext.Request.Method,
            traceId,
            MerchantReference);
    }

    return Results.Problem(
        title: "An unexpected error occurred.",
        statusCode: StatusCodes.Status500InternalServerError,
        extensions: new Dictionary<string, object?>
        {
            ["traceId"] = traceId
        }
    );
});

app.AdminBulkOrderEndpoints();
app.AdminOrderEndpoints();
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
app.MapRefundEndpoints();
app.MapControllers();
app.MapEmailEndpoints();
app.MapAdminRefundSyncEndpoints();


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