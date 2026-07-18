using System.ComponentModel.DataAnnotations;

namespace IdentityManagementSystem.UI.Areas.JudiciaryInquiry.ViewModels
{
    public class CartableItemViewModel
    {
        public long ItemId { get; set; }
        public long RequestId { get; set; }
        public string? NationalId { get; set; }
        public string? DocumentNumber { get; set; }
        public string? VerificationCode { get; set; }
        public string? MobileNumber { get; set; }
        public bool IdentityVerified { get; set; }
        public bool DocumentVerified { get; set; }
        public bool DocumentMatch { get; set; }
        public bool? ValidateByExpert { get; set; } // اضافه شده
        public string? Description { get; set; }
        public bool TextApproved { get; set; }
        public string? RequestStatus { get; set; }
        public long? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ViewedAt { get; set; }
        public string? Status { get; set; }
        public bool? IsMatch { get; internal set; }
        public bool? IsExist { get; internal set; }
        public bool? IsNationalIdInResponse { get; set; }
        public bool? IsNationalIdInLawyers { get; set; }
        public DateTime CreatedAt { get; internal set; }
        public string? ImpotrtantAnnexText { get; set; }
    }

    public class AssignCartableItemViewModel
    {
        [Required]
        public long ItemId { get; set; }
        [Required]
        public long AssignedTo { get; set; }
    }

    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
