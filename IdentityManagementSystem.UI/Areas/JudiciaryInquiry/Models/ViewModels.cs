using IdentityManagementSystem.Shared.DTO;

namespace IdentityManagementSystem.UI.Areas.JudiciaryInquiry.Models
{
    public class ApiResponseWrapper
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public CombinedResultData Data { get; set; }
    }

    public class CombinedResultData
    {
        public ShahkarResponse Shahkar { get; set; }
        public VerifyDocResponse VerifyDoc { get; set; }
        public long RequestId { get; set; }
    }

    public class VerifyDocResponse
    {
        public bool IsSuccessful { get; set; }
        public string ResponseText { get; set; }
        public List<PersonInQuery> PersonsInQuery { get; set; }
        public bool ExistDoc { get; set; }
        public bool IsNationalIdInLawyers { get; set; }
        public bool IsNationalIdInResponse { get; set; }
    }

    public class PersonInQuery
    {
        public string NationalNo { get; set; }
        public string Name { get; set; }
        public string Family { get; set; }
        public string RoleType { get; set; }
        // سایر خصوصیات
    }

    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    public class PaginatedCartableViewModel
    {
        public List<CartableItemViewModel> Items { get; set; } = new List<CartableItemViewModel>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public string SearchQuery { get; set; } = string.Empty;
    }

    public class CartableFormViewModel
    {
        public string NationalCode { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string VerifyCode { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string ClientNationalCode { get; set; } = string.Empty;
        public bool AgreeToTerms { get; set; }
        public string Step1Message { get; set; } = string.Empty;
        public string Step2Message { get; set; } = string.Empty;
        public string Step3Message { get; set; } = string.Empty;
    }

    public class CartableItemViewModel
    {
        public long RequestId { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
        public string ImpotrtantAnnexText { get; set; } = string.Empty;
        public bool? IsMatch { get; set; }
        public bool? IsExist { get; set; }
        public bool? IsNationalIdInResponse { get; set; }
        public bool? IsNationalIdInLawyers { get; set; }
        public bool? ValidateByExpert { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public bool IsRead { get; set; } // اضافه شده برای وضعیت خوانده شدن سند
        public string RejectReasonDisplay { get; set; } = string.Empty;
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class DocTextResponse
    {
        public bool Success { get; set; }
        public string? DocumentText { get; set; }
        public bool IsRead { get; set; }
        public string? Message { get; set; }
    }
}
