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
        public string NationalId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class ForgotPasswordVerifyViewModel : ForgotPasswordStartViewModel
    {
        public string Code { get; set; } = string.Empty;
    }

    public class ForgotPasswordResetViewModel
    {
        public string ResetToken { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
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
