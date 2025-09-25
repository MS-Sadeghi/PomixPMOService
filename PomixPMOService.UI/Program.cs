using DNTCaptcha.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ثبت HttpClient برای API با تمام تنظیمات
builder.Services.AddHttpClient("PomixApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5066/api/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.ConnectionClose = true; // کمک به رفع مشکلات اتصال
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true; // نادیده گرفتن SSL برای تست
    return handler;
});

builder.Services.AddDNTCaptcha(options =>
{
    options.UseCookieStorageProvider()
           .ShowThousandsSeparators(false)
           .WithEncryptionKey("YourEncryptionKey")
           .AbsoluteExpiration(minutes: 7);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=LoginPage}");



app.Run();