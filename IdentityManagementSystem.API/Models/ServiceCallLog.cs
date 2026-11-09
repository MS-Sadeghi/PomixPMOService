using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityManagementSystem.API.Models
{
    /// <summary>
    /// ثبت هر فراخوانی سرویس Pomix: ارسال، پاسخ، خطا یا انقضای زمان.
    /// </summary>
    [Table("ServiceCallLog", Schema = "Log")]
    public class ServiceCallLog
    {
        public const string StageSent = "Sent";
        public const string StageCompleted = "Completed";
        public const string StageFailed = "Failed";
        public const string StageTimedOut = "TimedOut";

        [Key]
        public long LogId { get; set; }

        [MaxLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Endpoint { get; set; }

        [MaxLength(2000)]
        public string? RequestParams { get; set; }

        public long? UserId { get; set; }

        [MaxLength(100)]
        public string? Username { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(64)]
        public string? TraceId { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }

        public int? DurationMs { get; set; }

        [MaxLength(20)]
        public string Stage { get; set; } = StageSent;

        public int? HttpStatusCode { get; set; }

        public int? PomixStatusCode { get; set; }

        public int? ResponseSize { get; set; }

        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }
    }
}
