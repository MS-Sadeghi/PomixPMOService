namespace IdentityManagementSystem.UI.Areas.Security.Models
{
    public class ServiceLogsPageViewModel
    {
        public List<ServiceLogSummaryViewModel> Summary { get; set; } = new();
        public List<ServiceLogItemViewModel> Items { get; set; } = new();

        // فیلترهای فعلی
        public string? Service { get; set; }
        public string? Stage { get; set; }
        public int PageSize { get; set; } = 50;

        // صفحه‌بندی و مصرف سهمیه (از API)
        public int Page { get; set; } = 1;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; } = 1;
        public int TodayCount { get; set; }
        public int DailyLimit { get; set; }
    }

    public class ServiceLogSummaryViewModel
    {
        public string ServiceName { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
        public int TimedOut { get; set; }
        public int Stuck { get; set; }
        public double AvgDurationMs { get; set; }
        public DateTime? LastCallAt { get; set; }
    }

    public class ServiceLogItemViewModel
    {
        public long LogId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string? RequestParams { get; set; }
        public string? Username { get; set; }
        public string? IpAddress { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? DurationMs { get; set; }
        public string Stage { get; set; } = string.Empty;
        public int? HttpStatusCode { get; set; }
        public int? PomixStatusCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
