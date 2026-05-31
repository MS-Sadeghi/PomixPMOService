using DNTCaptcha.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using IdentityManagementSystem.UI.Filters;



var builder = WebApplication.CreateBuilder(args);

// پیکربندی سرویس‌ها
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/LoginPage";
        options.AccessDeniedPath = "/Error/AccessDenied";
        options.ReturnUrlParameter = "returnUrl";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// خواندن تنظیمات از appsettings.json
var apiUrl = builder.Configuration["ApiSettings:URL"];
var apiKey = builder.Configuration["ApiSettings:ApiKey"];

builder.Services.AddHttpClient("PomixApi", client =>
{
    client.BaseAddress = new Uri($"{apiUrl}/api/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("X-API-Key", apiKey); // اضافه کردن کلید API
    client.DefaultRequestHeaders.ConnectionClose = true;
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
    return handler;
});


builder.Services.AddHttpClient("PomixApiPublic", client =>
{
    client.BaseAddress = new Uri($"{apiUrl}/api/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.ConnectionClose = true;
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
    return handler;
});


builder.Services.AddDNTCaptcha(options =>
{
    options.UseCookieStorageProvider()  // بدون هیچ پارامتری
                                        // .UseSessionStorageProvider()  // گزینه جایگزین - امن‌تر
                                        // .UseMemoryCacheStorageProvider()  // گزینه جایگزین - متکی به زمان سرور
           .ShowThousandsSeparators(false)
           .WithEncryptionKey("YourEncryptionKey")
           .AbsoluteExpiration(minutes: 7);
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new NoCacheFilterAttribute());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // برای فایل‌های استاتیک (CSS، JS و غیره)
app.UseRouting();
app.UseCors("AllowAll");
app.UseSession(); // Session باید قبل از middleware سفارشی باشد
app.UseAuthentication();
app.UseAuthorization();

// Middleware سفارشی برای بررسی JwtToken
app.Use(async (context, next) =>
{
    // رد کردن مسیرهای مربوط به فایل‌های استاتیک و ورود
    var path = context.Request.Path.Value?.ToLower();

    // مسیرهایی که نباید چک بشن (API + Static + Login)
    if (path.StartsWith("/home/loginpage") ||
        path.StartsWith("/api") ||
        path.StartsWith("/swagger") ||
        path.StartsWith("/css") ||
        path.StartsWith("/js") ||
        path.StartsWith("/lib") ||
        path.StartsWith("/assets") ||
        path.Contains("captcha"))
    {
        await next();
        return;
    }

    // فقط برای صفحات UI اصلی (MVC)
    var hasToken = context.Session.GetString("JwtToken");

    if (string.IsNullOrEmpty(hasToken))
    {
        // فقط اگر کاربر واقعاً در حال Browse UI هست
        if (context.Request.Headers["Accept"].ToString().Contains("text/html"))
        {
            context.Response.Redirect("/Home/LoginPage");
            return;
        }

        // برای API یا Ajax → 401 بده، نه redirect
        context.Response.StatusCode = 401;
        return;
    }

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Cartable}/{action=Index}/{id?}");

app.Run();