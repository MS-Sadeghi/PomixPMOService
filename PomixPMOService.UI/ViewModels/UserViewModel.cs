using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PomixPMOService.UI.ViewModels
{
    public class UserViewModel
    {

        public long UserId { get; set; }
        public string NationalCode { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? PasswordSalt { get; set; }       // اختیاری، می‌تونه null باشه
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public required string Role { get; set; }                 // ارتباط به جدول Roles
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }       // می‌تونه null باشه
        public bool IsActive { get; set; }
        public string? MobileNumber { get; set; }      // اختیاری، می‌تونه null باشه
    }

    public class CreateUserViewModel
    {
        [Required]
        [StringLength(10)]
        public string? NationalId { get; set; }
        [Required]
        [StringLength(75)]
        public string? Name { get; set; }
        [Required]
        [StringLength(85)]
        public string? LastName { get; set; }
        [Required]
        [StringLength(255)]
        public string? Password { get; set; }

        [Required]
        [StringLength(50)]
        public string? Username { get; set; }

        [Required]
        [StringLength(50)]
        public string? Role { get; set; }
    }

    public class LoginViewModel
    {
        [Required]
        [StringLength(50)]
        public string? Username { get; set; }

        [Required]
        [StringLength(255)]
        public string? Password { get; set; }
    }
    public class GrantAccessViewModel
    {
        public long UserId { get; set; }
        [Required]
        [StringLength(50)]
        public string Permission { get; set; }
    }
}
