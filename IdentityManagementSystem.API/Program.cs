using IdentityManagementSystem.API.Controllers;
using IdentityManagementSystem.API.Data;
using IdentityManagementSystem.API.Infrastructure;
using IdentityManagementSystem.API.Modules.AccessControlReports;
using IdentityManagementSystem.API.Modules.AccessControlReports.Common;
using IdentityManagementSystem.API.Services;
using IdentityManagementSystem.API.Services.Logging;
using IdentityManagementSystem.API.Services.SMS;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- DbContext ---
builder.Services.AddDbContext<IdentityManagementSystemContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TokenService, TokenService>();
builder.Services.Configure<ShahkarServiceOptions>(builder.Configuration.GetSection("Shahkar"));

// --- AccessControlReports Modules ---
builder.Services.Configure<PomixOptions>(
    builder.Configuration.GetSection("Pomix"));
builder.Services.AddAccessControlReports();


// --- Authentication & Authorization ---
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanAccessShahkar", policy =>
        policy.RequireClaim("Permission", "CanAccessShahkar"));


    options.AddPolicy("CanValidateRequest", policy =>
        policy.RequireClaim("Permission", "CanValidateRequest"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserActionLogger>();
builder.Services.Configure<DiafaanSmsSettings>(builder.Configuration.GetSection("DiafanSmsSettings"));
builder.Services.Configure<RajaeiSmsSettings>(builder.Configuration.GetSection("RajaeiSmsSettings"));
builder.Services.AddHttpClient<SendServiceClient>();
builder.Services.AddScoped<SendSmsService>();

// --- Swagger ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "ServicePomixPMO API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "لطفاً توکن JWT را وارد کنید: Bearer {token}",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// --- CORS ---
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUI", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// --- Middleware ---
app.UseGlobalExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ServicePomixPMO API V1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

app.UseCors("AllowUI");


if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
