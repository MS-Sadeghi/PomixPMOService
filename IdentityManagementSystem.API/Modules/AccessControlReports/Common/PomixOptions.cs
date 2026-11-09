namespace IdentityManagementSystem.API.Modules.AccessControlReports.Common
{
    public class PomixOptions
    {
        public string BaseUrl { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        // حداکثر زمان انتظار برای پاسخ سرویس Pomix (ثانیه)
        public int TimeoutSeconds { get; set; } = 30;

        // سقف مجاز فراخوانی در روز (طبق محدودیت Pomix)؛ ۰ یعنی بررسی سهمیه غیرفعال است.
        // فقط هشدار لاگ می‌دهد و هیچ فراخوانی‌ای را رد نمی‌کند.
        public int DailyCallLimit { get; set; } = 600;

        // با رسیدن مصرف روزانه به این درصد از سقف، هشدار ثبت می‌شود
        public int QuotaWarnPercent { get; set; } = 80;

        // رکوردهای Sent قدیمی‌تر از این مقدار (دقیقه) به Abandoned تبدیل می‌شوند
        public int StaleSentMinutes { get; set; } = 5;
    }

    public class PomixResult
    {
        public bool IsSuccessful { get; set; }

        public int ResponseStatusCode { get; set; }

        public string ResponseText { get; set; } = string.Empty;
    }
}