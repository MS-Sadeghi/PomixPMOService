using System.ComponentModel.DataAnnotations;

namespace IdentityManagementSystem.UI.Areas.Security.Models
{
    public class ChangePasswordViewModel
    {
        public string? CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
    }

    public class UserProfileViewModel
    {
        public string Name { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }
    }

    public class RoleInfo
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
    }

    public class LoginResponse
    {
        public long UserId { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public RoleInfo Role { get; set; }
        public TokenInfo Tokens { get; set; }
    }

    public class UserInfo
    {
        public long UserId { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }
    }

    public class RoleViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
    }

    public class TokenInfo
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public class UpdateUserViewModel
    {
        public long UserId { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string NationalId { get; set; }
        public string MobileNumber { get; set; }
        public int RoleId { get; set; }
        public bool IsActive { get; set; }
    }

    public class ForgotPasswordStartViewModel
    {
        [Required(ErrorMessage = "کد ملی الزامی است.")]
        [RegularExpression("^\\d{10}$", ErrorMessage = "کد ملی باید ۱۰ رقم باشد.")]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره همراه الزامی است.")]
        [RegularExpression("^09\\d{9}$", ErrorMessage = "شماره همراه باید با 09 شروع شود و ۱۱ رقم باشد.")]
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class ForgotPasswordVerifyViewModel : ForgotPasswordStartViewModel
    {
        [Required(ErrorMessage = "کد تأیید الزامی است.")]
        [RegularExpression("^\\d{5}$", ErrorMessage = "کد تأیید باید ۵ رقم باشد.")]
        public string Code { get; set; } = string.Empty;
    }

    public class ForgotPasswordResetViewModel
    {
        [Required(ErrorMessage = "نشست بازنشانی نامعتبر است.")]
        public string ResetToken { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور جدید الزامی است.")]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[^\\da-zA-Z]).{8,}$", ErrorMessage = "رمز عبور باید حداقل ۸ کاراکتر و شامل حرف بزرگ، حرف کوچک، عدد و نویسه خاص باشد.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأیید رمز عبور الزامی است.")]
        [Compare(nameof(NewPassword), ErrorMessage = "رمز عبور و تأیید آن یکسان نیستند.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public class ForgotPasswordApiResponse
    {
        public string? Message { get; set; }
        public string? ResetToken { get; set; }
        public string? DevelopmentCode { get; set; }
        public int? ExpiresInSeconds { get; set; }
    }
}
