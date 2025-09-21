using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PomixPMOService.API.Models.ViewModels
{
    public class UserViewModel
    {
        public long UserId { get; set; }
        [Required]
        [StringLength(10)]
        public string? NationalId { get; set; }
        [Required]
        [StringLength(11)]
        [Column("mobile_number")]
        public string? MobileNumber { get; set; }
        [Required]
        [StringLength(50)]
        public string? Username { get; set; }
        [Required]
        [StringLength(75)]
        public string? Name { get; set; }
        [Required]
        [StringLength(85)]
        public string? LastName { get; set; }
        [Required]
        [StringLength(50)]
        public string? Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool IsActive { get; set; }
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
