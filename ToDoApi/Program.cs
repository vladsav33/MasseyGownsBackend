using GownsApi;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using GownApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("GownDb");

//Register the DbContext to use SQL Server (Azure SQL)
builder.Services.AddDbContext<GownDb>(options =>
    options
           .UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddEndpointsApiExplorer(); // Required for minimal APIs
builder.Services.AddSwaggerGen();           // Adds Swagger generation

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173",
                               "https://masseygowns-bzhkgzfubkavgchw.newzealandnorth-01.azurewebsites.net")  // allow frontend to prevent CORS errors
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

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

//app.MapGet("/todoitems/complete", async (GownDb db) =>
//    await db.degree.Where(t => t.IsComplete).ToListAsync());

app.Run();
