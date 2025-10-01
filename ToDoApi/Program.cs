using GownApi;
using Microsoft.EntityFrameworkCore;
using GownApi.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

app.Run();
