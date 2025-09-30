using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace ServicePomixPMO.API.Models
{
    public class User
    {
        [Key]
        public long UserId { get; set; }
        public string NationalId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // فقط foreign key
        public int RoleId { get; set; }

        // navigation property
        public Role Role { get; set; } = null!;

        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
        public bool IsActive { get; set; } = true;
    }


    public class Request
    {
        [Key]
        public long RequestId { get; set; }

      
        [StringLength(10)]
        public string? NationalId { get; set; }     // کد ملی

        [StringLength(20)]
        public string? MobileNumber { get; set; }   // شماره همراه

        [StringLength(50)]
        public string? DocumentNumber { get; set; } // شناسه سند

        [StringLength(50)]
        public string? VerificationCode { get; set; } // رمز تصدیق
        public bool? ValidateByExpert { get; set; } // اضافه شده
        public string? Description { get; set; }
        public bool? IsMatch { get; set; }              // وضعیت احراز هویت
        public bool? IsExist { get; set; }              // وجود سند
        public bool? IsNationalIdInResponse { get; set; } // بررسی نهایی

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string? UpdatedBy { get; set; }
        public string RequestCode { get; set; } = string.Empty;
    }

    public class Cartable
    {
        [Key]
        public long CartableId { get; set; }

        [ForeignKey("User")]
        public long UserId { get; set; }

        public User? User { get; set; }
        public string? CartableName { get; set; }
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
        public bool? ValidateByExpert { get; set; } // اضافه شده
        public string? Description { get; set; }
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
        public int? UserId { get; set; }

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

      
        public long RequestId { get; set; }

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

        public long RequestId { get; set; }
        [Required]
        public string VerificationCode { get; set; } = null!; // SecretNo

        // تاریخ و کاربر ایجاد کننده
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Required]
        public string CreatedBy { get; set; } = null!;
        public bool? IsExist { get;  set; }
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
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

}