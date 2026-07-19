using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;

namespace IdentityManagementSystem.UI.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDNTCaptchaValidatorService _captchaValidatorService;
        private readonly HttpClient _client;

        public HomeController(
            IHttpClientFactory httpClientFactory,
            IDNTCaptchaValidatorService captchaValidatorService)
        {
            _client = httpClientFactory.CreateClient("PomixApi");
            _captchaValidatorService = captchaValidatorService ?? throw new ArgumentNullException(nameof(captchaValidatorService));
        }

        [HttpGet]
        public IActionResult Index()
        {
			HttpContext.Session.Remove("CurrentModule"); 
            return View();
        }

    }

    #region ViewModels
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
    #endregion
}
