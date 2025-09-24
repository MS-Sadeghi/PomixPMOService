using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServicePomixPMO.API.Models
{
    public class User
    {
        [Key]
        [Column("userid")]
        public long UserId { get; set; }

        [Required]
        [StringLength(10)]
        [Column("national_code")] // Map به ستون دیتابیس
        public string? NationalId { get; set; }

        [Required]
        [StringLength(11)]
        [Column("mobile_number")]
        public string? MobileNumber { get; set; }

        [Required]
        [StringLength(50)]
        [Column("username")]
        public string? Username { get; set; }

        [Required]
        [StringLength(255)]
        [Column("password_hash")] // Map به ستون دیتابیس
        public string? PasswordHash { get; set; }

        [Required]
        [StringLength(75)]
        [Column("name")]
        public string? Name { get; set; }

        [Required]
        [StringLength(85)]
        [Column("lastname")]
        public string? LastName { get; set; }

        [Required]
        [StringLength(50)]
        [Column("role")]
        public string? Role { get; set; }

        [Column("created_at")] // Map به ستون دیتابیس
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_login")] // Map به ستون دیتابیس
        public DateTime? LastLogin { get; set; }

        [Column("is_active")] // Map به ستون دیتابیس
        public bool IsActive { get; set; } = true;
    }

    public class Request
    {

        [Key]
        [Column("request_id")]
        public long RequestId { get; set; }

        [Column("national_code")]
        public string NationalId { get; set; } = string.Empty;

        [Column("request_code")]
        public string RequestCode { get; set; } = string.Empty;

        [Column("document_number")]
        public string? DocumentNumber { get; set; } = string.Empty;

        [Column("verification_code")]
        public string VerificationCode { get; set; } = string.Empty;

        [Column("identity_verified")]
        public bool IdentityVerified { get; set; }

        [Column("document_verified")]
        public bool DocumentVerified { get; set; }

        [Column("document_match")]
        public bool DocumentMatch { get; set; }

        [Column("text_approved")]
        public bool TextApproved { get; set; }

        [Column("expert_id")]
        public long ExpertId { get; set; }

        [Column("request_status")]
        public string RequestStatus { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("document_text")]
        public string DocumentText { get; set; } = string.Empty;

        [Column("mobile_number")]
        public string MobileNumber { get; set; } = string.Empty;

        // Navigation property برای کارشناس/Expert
        [ForeignKey("ExpertId")]
        public virtual User Expert { get; set; } = null!;
    }

    public class Cartable
    {
        [Key]
        public long CartableId { get; set; }

        [ForeignKey("User")]
        public long UserId { get; set; }

        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CartableItem
    {
        [Key]
        public long ItemId { get; set; }

        [ForeignKey("Cartable")]
        public long CartableId { get; set; }

        public Cartable? Cartable { get; set; }

        [ForeignKey("Request")]
        public long RequestId { get; set; }

        public Request? Request { get; set; }

        [ForeignKey("User")]
        public long? AssignedTo { get; set; }

        public User? AssignedToUser { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ViewedAt { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "New";
    }

    public class UserLog
    {
        [Key]
        [Column("log_id")]
        public long LogId { get; set; }

        [Column("userid")]
        public long? UserId { get; set; }

        [Column("action")]
        public string? Action { get; set; }

        [Column("action_time")]
        public DateTime? ActionTime { get; set; }

        [Column("ip_address")]
        public string? IpAddress { get; set; }

        [Column("user_agent")]
        public string? UserAgent { get; set; }
    }

    public class RequestLog
    {
        [Key]
        public long LogId { get; set; }

        [ForeignKey("Request")]
        public long RequestId { get; set; }

        public Request? Request { get; set; }

        [ForeignKey("User")]
        public long? UserId { get; set; }

        public User? User { get; set; }

        [Required]
        [StringLength(50)]
        public string? Action { get; set; }

        public DateTime ActionTime { get; set; } = DateTime.UtcNow;

        public string? Details { get; set; }
    }

    // مدل جدید UserAccess
    public class UserAccess
    {
        [Key]
        [Column("AccessId")]
        public long Id { get; set; }

        [ForeignKey("User")]
        [Column("UserId")]
        public long UserId { get; set; }

        public User? User { get; set; }

        [Required]
        [StringLength(50)]
        [Column("Permission")]
        public string? Permission { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("ShahkarLog", Schema = "Log")]
    public class ShahkarLog
    {
        [Key]
        public long LogId { get; set; }

        [Required]
        [StringLength(10)]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        [StringLength(11)]
        public string MobileNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string RequestCode { get; set; } = string.Empty;

        [Required]
        public bool IsMatch { get; set; }

        public string? ResponseText { get; set; }

        [Required]
        public long ExpertId { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("VerifyDocLog", Schema = "Log")]
    public class VerifyDocLog
    {
        [Key]
        public int VerifyDocLogId { get; set; }

        // لاگ اصلی: کل JSON پاسخ سرویس
        [Required]
        public string ResponseText { get; set; } = null!;

        // مقادیر ورودی که بعدا برای تطابق کارتابل استفاده میشن
        [Required]
        public string DocumentNumber { get; set; } = null!;  // NationalRegisterNo

        [Required]
        public string VerificationCode { get; set; } = null!; // SecretNo

        // تاریخ و کاربر ایجاد کننده
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Required]
        public string CreatedBy { get; set; } = null!;
    }

    [Table("RefreshTokens", Schema = "Sec")]
    public class RefreshToken
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long UserId { get; set; }

        [Required]
        [StringLength(255)]
        public string Token { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiryDate { get; set; }

        public bool IsRevoked { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}