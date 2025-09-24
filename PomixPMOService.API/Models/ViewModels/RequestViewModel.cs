using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PomixPMOService.API.Models.ViewModels
{

    public class RequestViewModel
    {
        public long RequestId { get; set; }
        [Required]
        [StringLength(10)]
        public string? NationalId { get; set; }
        [Required]
        [StringLength(11)]
        [Column("mobile_number")]
        public string? MobileNumber { get; set; }
        [Required]
        [StringLength(50)]
        public string? DocumentNumber { get; set; }
        [Required]
        [StringLength(50)]
        public string? VerificationCode { get; set; }
        public bool IdentityVerified { get; set; }
        public bool DocumentVerified { get; set; }
        public bool DocumentMatch { get; set; }
        public bool TextApproved { get; set; }
        public bool IsMatch { get; set; }
        public long? ExpertId { get; set; }
        public string? ExpertName { get; set; }
        public string? RequestStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? DocumentText { get; set; }
    }

    public class CreateRequestViewModel
    {
        [Required]
        [StringLength(10)]
        public string? NationalId { get; set; }
        [Required]
        [StringLength(50)]
        public string? DocumentNumber { get; set; }
        [Required]
        [StringLength(50)]
        public string? VerificationCode { get; set; }
        public string? DocumentText { get; set; }
        public string MobileNumber { get; internal set; }
    }
}
