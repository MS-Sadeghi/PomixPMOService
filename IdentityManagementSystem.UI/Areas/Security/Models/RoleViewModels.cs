using System.ComponentModel.DataAnnotations;

namespace IdentityManagementSystem.UI.Areas.Security.Models
{
    public class RoleItemViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int UserCount { get; set; }
        public bool IsProtected { get; set; }
    }

    public class RoleFormViewModel
    {
        public int RoleId { get; set; }

        [Required(ErrorMessage = "نام نقش الزامی است.")]
        [StringLength(200, ErrorMessage = "نام نقش نباید بیش از ۲۰۰ کاراکتر باشد.")]
        public string? RoleName { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
