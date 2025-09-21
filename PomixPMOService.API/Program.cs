using Microsoft.EntityFrameworkCore;
using PomixPMOService.API.Controllers;
using ServicePomixPMO.API.Data;

var builder = WebApplication.CreateBuilder(args);

// --- DbContext ---
builder.Services.AddDbContext<PomixServiceContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.Configure<ShahkarServiceOptions>(builder.Configuration.GetSection("Shahkar"));

// --- Swagger ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "ServicePomixPMO API", Version = "v1" });
});

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("http://localhost:3000")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

// --- Authentication & Authorization قبل از Build ---
builder.Services.AddAuthentication("CustomScheme")
    .AddCookie("CustomScheme", options =>
    {
        options.LoginPath = "/api/auth/login";
        options.AccessDeniedPath = "/api/auth/denied";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// --- Middleware ---
app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowFrontend");
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ServicePomixPMO API V1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.Run();
