using DNTCaptcha.Core;
using IdentityManagementSystem.UI.Areas.Security.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace IdentityManagementSystem.UI.Areas.Security.Controllers
{
    [Area("Security")]
    public class AccountController : Controller
    {
        private readonly IDNTCaptchaValidatorService _captchaValidatorService;
        private readonly HttpClient _client;

        public AccountController(
            IHttpClientFactory httpClientFactory,
            IDNTCaptchaValidatorService captchaValidatorService)
        {
            _client = httpClientFactory.CreateClient("PomixApi");
            _captchaValidatorService = captchaValidatorService ?? throw new ArgumentNullException(nameof(captchaValidatorService));
        }

        #region Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!_captchaValidatorService.HasRequestValidCaptchaEntry())
            {
                return Json(new { success = false, message = "کد امنیتی اشتباه است." });
            }

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "لطفاً همه فیلدها را وارد کنید." });
            }

            try
            {
                var response = await _client.PostAsJsonAsync("auth/login", model);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return Json(new { success = false, message = "نام کاربری یا رمز عبور نامعتبر است." });
                    }

                    return Json(new { success = false, message = "ارتباط با سرور برقرار نشد. لطفاً بعداً دوباره تلاش کنید." });
                }

                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

                if (loginResponse?.Tokens?.AccessToken == null)
                {
                    return Json(new { success = false, message = "توکن دریافت نشد." });
                }

                HttpContext.Session.SetString("JwtToken", loginResponse.Tokens.AccessToken);
                HttpContext.Session.SetString("RefreshToken", loginResponse.Tokens.RefreshToken ?? "");

                HttpContext.Session.SetString("UserRole", loginResponse.Role?.RoleName ?? "");
                HttpContext.Session.SetString("UserRoleId", loginResponse.Role?.RoleId.ToString() ?? "0");
                HttpContext.Session.SetString("UserName", loginResponse.Name ?? "");
                HttpContext.Session.SetString("UserLastName", loginResponse.LastName ?? "");
                HttpContext.Session.SetString("UserId", loginResponse.UserId.ToString());
                HttpContext.Session.SetString("Username", loginResponse.Username ?? "");

                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action(
                        "Dashboard",
                        "Report",
                        new { area = "AccessControlReports" })
                });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }
        #endregion

        #region Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                var refreshToken = HttpContext.Session.GetString("RefreshToken");

                HttpContext.Session.Remove("JwtToken");
                HttpContext.Session.Remove("RefreshToken");
                HttpContext.Session.Clear();

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(refreshToken))
                {
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await _client.PostAsJsonAsync("auth/refresh/revoke", new { RefreshToken = refreshToken });
                    if (!response.IsSuccessStatusCode)
                    {
                        //Console.WriteLine($"Failed to revoke refresh token: {await response.Content.ReadAsStringAsync()}");
                    }
                }

                TempData["SuccessLogoutMessage"] = "شما با موفقیت از سیستم خارج شدید.";
                return RedirectToAction("Login");
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید.";
                return RedirectToAction("Login");
            }
        }
        #endregion

        #region Users
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "لطفاً ابتدا وارد سیستم شوید.";
                    return RedirectToAction("Login");
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _client.GetAsync("Auth/GetUsers");
                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<UserViewModel>>();
                    return View(users ?? new List<UserViewModel>());
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = "خطا در دریافت کاربران.";
                    return View(new List<UserViewModel>());
                }
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید.";
                return View(new List<UserViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(" | ", errors) });
                }

                if (model.Password != model.ConfirmPassword)
                {
                    return Json(new { success = false, message = "رمز عبور و تأیید رمز عبور یکسان نیستند" });
                }

                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "لطفاً ابتدا وارد سیستم شوید" });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _client.PostAsJsonAsync("Auth/register", new
                {
                    model.Name,
                    model.LastName,
                    model.Username,
                    model.Password,
                    model.NationalId,
                    model.MobileNumber,
                    model.RoleId
                });

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "کاربر با موفقیت ایجاد شد" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = "خطا در ایجاد کاربر." });
                }
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(long id)
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "لطفاً ابتدا وارد سیستم شوید." });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _client.GetAsync($"Auth/GetUsers");
                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<UserViewModel>>();
                    var user = users?.FirstOrDefault(u => u.UserId == id);
                    if (user == null)
                    {
                        return Json(new { success = false, message = "کاربر یافت نشد." });
                    }
                    return Json(new
                    {
                        success = true,
                        userId = user.UserId,
                        name = user.Name,
                        lastName = user.LastName,
                        username = user.Username,
                        nationalId = user.NationalId,
                        mobileNumber = user.MobileNumber,
                        roleId = user.RoleId,
                        isActive = user.IsActive
                    });
                }
                return Json(new { success = false, message = "خطا در دریافت اطلاعات کاربر" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(UpdateUserViewModel model)
        {
            try
            {
                if (!string.IsNullOrEmpty(model.Password) && model.Password != model.ConfirmPassword)
                {
                    return Json(new { success = false, message = "رمز عبور و تأیید رمز عبور یکسان نیستند" });
                }

                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "لطفاً ابتدا وارد سیستم شوید" });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // ساختن آبجکت برای ارسال به API
                var updateData = new
                {
                    model.Name,
                    model.LastName,
                    model.Username,
                    Password = string.IsNullOrEmpty(model.Password) ? null : model.Password,
                    model.NationalId,
                    model.MobileNumber,
                    model.RoleId
                };

                var response = await _client.PutAsJsonAsync($"Auth/UpdateUser/{model.UserId}", updateData);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "کاربر با موفقیت به‌روزرسانی شد" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = "خطا در به‌روزرسانی کاربر." });
                }
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDeleteUser(long id)
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "توکن یافت نشد. لطفاً دوباره وارد سیستم شوید." });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _client.DeleteAsync($"Auth/SoftDeleteUser/{id}");

                if (response.IsSuccessStatusCode)
                {
                    // خواندن پیام از پاسخ API
                    var result = await response.Content.ReadAsStringAsync();
                    return Json(new { success = true, message = result ?? "کاربر با موفقیت غیرفعال شد." });
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = "خطا در غیرفعال کردن کاربر." });
                }
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreUser(long id)
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "لطفاً ابتدا وارد سیستم شوید" });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _client.PostAsync($"Auth/RestoreUser/{id}", null);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "کاربر با موفقیت فعال شد" });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = "خطا در فعال‌سازی کاربر." });
                }
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserProfile()
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                {
                    return Json(new { success = false, message = "توکن یافت نشد." });
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _client.GetAsync("Auth/GetCurrentUser");

                if (response.IsSuccessStatusCode)
                {
                    var userInfo = await response.Content.ReadFromJsonAsync<UserProfileViewModel>();
                    return Json(new
                    {
                        success = true,
                        name = userInfo.Name,
                        lastName = userInfo.LastName,
                        role = userInfo.Role
                    });
                }

                return Json(new { success = false, message = "خطا در دریافت اطلاعات کاربر" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var token = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(token))
                    return Unauthorized(); // یا return Json(new List<RoleViewModel>());

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _client.GetAsync("Auth/GetRoles");

                if (response.IsSuccessStatusCode)
                {
                    var roles = await response.Content.ReadFromJsonAsync<List<RoleViewModel>>();
                    return Ok(roles); // فقط لیست رو برگردون
                }

                return Ok(new List<RoleViewModel>()); // لیست خالی
            }
            catch (Exception)
            {
                return Ok(new List<RoleViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "لطفاً همه فیلدها را به درستی پر کنید." });
            }

            if (model.NewPassword != model.ConfirmNewPassword)
            {
                return Json(new { success = false, message = "رمز عبور جدید و تأیید رمز عبور یکسان نیستند." });
            }

            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
            {
                return Json(new { success = false, message = "لطفاً ابتدا وارد سیستم شوید." });
            }

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.PostAsJsonAsync("Auth/ChangePassword", model);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "رمز عبور با موفقیت تغییر کرد." });
            }

            var error = await response.Content.ReadAsStringAsync();
            var errorObj = JsonConvert.DeserializeObject<dynamic>(error);
            return Json(new { success = false, message = "خطا در تغییر رمز عبور." });
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var userInfo = new UserInfo
            {
                UserId = long.Parse(HttpContext.Session.GetString("UserId") ?? "0"),
                Username = HttpContext.Session.GetString("Username") ?? "",
                Name = HttpContext.Session.GetString("UserName") ?? "",
                LastName = HttpContext.Session.GetString("UserLastName") ?? "",
                Role = HttpContext.Session.GetString("UserRole") ?? "بدون نقش"
            };

            if (userInfo.UserId == 0)
            {
                ViewBag.ErrorMessage = "لطفاً ابتدا وارد سیستم شوید.";
                return RedirectToAction("Login");
            }

            return View(userInfo);
        }

        #endregion

        #region ForgotPassword
        [AllowAnonymous]
        public IActionResult ForgotPassword(ForgotPasswordStartViewModel model)
        {
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartForgotPassword(ForgotPasswordStartViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = GetFirstModelError() ?? "اطلاعات وارد شده معتبر نیست." });

            if (!IsValidNationalId(model.NationalId) || !IsValidMobileNumber(model.MobileNumber))
                return Json(new { success = false, message = "کد ملی یا شماره همراه معتبر نیست." });

            try
            {
                var response = await _client.PostAsJsonAsync("Auth/forgot-password/start", new
                {
                    model.NationalId,
                    model.MobileNumber
                });

                var apiResult = await ReadForgotPasswordApiResponse(response);
                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = apiResult?.Message ?? "در صورت تطابق اطلاعات، کد تأیید ارسال می‌شود."
                    });
                }

                return Json(new { success = false, message = apiResult?.Message ?? "امکان ارسال کد تأیید وجود ندارد." });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyForgotPasswordCode(ForgotPasswordVerifyViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = GetFirstModelError() ?? "کد تأیید معتبر نیست." });

            if (!IsValidNationalId(model.NationalId) || !IsValidMobileNumber(model.MobileNumber) || !Regex.IsMatch(model.Code ?? "", "^\\d{5}$"))
                return Json(new { success = false, message = "کد تأیید معتبر نیست." });

            try
            {
                var response = await _client.PostAsJsonAsync("Auth/forgot-password/verify", new
                {
                    model.NationalId,
                    model.MobileNumber,
                    model.Code
                });

                var apiResult = await ReadForgotPasswordApiResponse(response);
                if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(apiResult?.ResetToken))
                    return Json(new { success = true, message = apiResult.Message ?? "کد تأیید شد.", resetToken = apiResult.ResetToken });

                return Json(new { success = false, message = apiResult?.Message ?? "کد تأیید نامعتبر یا منقضی شده است." });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetForgotPassword(ForgotPasswordResetViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = GetFirstModelError() ?? "اطلاعات بازنشانی رمز عبور معتبر نیست." });

            if (string.IsNullOrWhiteSpace(model.ResetToken) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                model.NewPassword != model.ConfirmNewPassword ||
                !IsStrongPassword(model.NewPassword))
            {
                return Json(new { success = false, message = "رمز عبور باید حداقل ۸ کاراکتر و شامل حرف بزرگ، حرف کوچک، عدد و نویسه خاص باشد." });
            }

            try
            {
                var response = await _client.PostAsJsonAsync("Auth/forgot-password/reset", new
                {
                    model.ResetToken,
                    model.NewPassword,
                    model.ConfirmNewPassword
                });

                var apiResult = await ReadForgotPasswordApiResponse(response);
                if (response.IsSuccessStatusCode)
                    return Json(new { success = true, message = apiResult?.Message ?? "رمز عبور با موفقیت تغییر کرد.", redirectUrl = Url.Action("Login", "Account", new { area = "Security" }) });

                return Json(new { success = false, message = apiResult?.Message ?? "نشست بازنشانی نامعتبر یا منقضی شده است." });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." });
            }
        }

        private static async Task<ForgotPasswordApiResponse?> ReadForgotPasswordApiResponse(HttpResponseMessage response)
        {
            try
            {
                return await response.Content.ReadFromJsonAsync<ForgotPasswordApiResponse>();
            }
            catch (Exception)
            {
                return new ForgotPasswordApiResponse
                {
                    Message = await response.Content.ReadAsStringAsync()
                };
            }
        }

        private string? GetFirstModelError()
        {
            return ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
        }

        #endregion

        private static bool IsValidNationalId(string? nationalId)
            => !string.IsNullOrWhiteSpace(nationalId) && Regex.IsMatch(nationalId, "^\\d{10}$");

        private static bool IsValidMobileNumber(string? mobileNumber)
            => !string.IsNullOrWhiteSpace(mobileNumber) && Regex.IsMatch(mobileNumber, "^09\\d{9}$");

        private static bool IsStrongPassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            return password.Any(char.IsLower) &&
                   password.Any(char.IsUpper) &&
                   password.Any(char.IsDigit) &&
                   password.Any(ch => !char.IsLetterOrDigit(ch));
        }
    }
}
