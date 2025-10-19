using DNTCaptcha.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PomixPMOService.UI.Filters;

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

builder.Services.AddHttpClient("PomixApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5066/api/");
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
    options.UseCookieStorageProvider(SameSiteMode.None)  // برای CORS، یا Lax اگر لازم
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
    if (context.Request.Path.StartsWithSegments("/Home/LoginPage") ||
    context.Request.Path.StartsWithSegments("/css") ||
    context.Request.Path.StartsWithSegments("/js") ||
    context.Request.Path.StartsWithSegments("/lib") ||
    context.Request.Path.StartsWithSegments("/assets") ||
    context.Request.Path.StartsWithSegments("/DNTCaptcha") ||  // موجود
    context.Request.Path.StartsWithSegments("/DNTCaptchaImage") ||  // اضافه کنید
    context.Request.Path.Value.Contains("captcha"))  // برای ایمنی بیشتر
    {
        await next();
        return;
    }

    // بررسی session برای JwtToken
    if (!context.Session.TryGetValue("JwtToken", out _) || string.IsNullOrEmpty(context.Session.GetString("JwtToken")))
    {
        if (context.User.Identity.IsAuthenticated)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Session.Clear();
            Console.WriteLine("Session timeout detected, user signed out.");
        }

        context.Response.Redirect("/Home/LoginPage");
        return;
    }

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Cartable}/{action=Index}/{id?}");

app.Run();