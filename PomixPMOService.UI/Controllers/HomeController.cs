using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;
using PomixPMOService.UI.ViewModels;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;

namespace PomixPMOService.UI.Controllers
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

        #region Login

        [HttpGet]
        public IActionResult LoginPage()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginPage(LoginViewModel model)
        {
            if (!_captchaValidatorService.HasRequestValidCaptchaEntry())
            {
                ModelState.AddModelError("", "کد امنیتی اشتباه است.");
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "لطفا همه فیلدها را وارد کنید.";
                return View(model);
            }

            try
            {
                var response = await _client.PostAsJsonAsync("auth/login", model);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

                    if (loginResponse?.Tokens?.AccessToken != null)
                    {
                        // ذخیره توکن و redirect
                        HttpContext.Session.SetString("JwtToken", loginResponse.Tokens.AccessToken);
                        return RedirectToAction("Index", "Cartable");
                    }
                    else
                    {
                        ViewBag.ErrorMessage = "خطا: توکن دریافت نشد.";
                        return View(model); // ← مسیر جایگزین برای وقتی توکن null است
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ViewBag.ErrorMessage = "خطا در ورود: " + error;
                    return View(model); // ← مسیر جایگزین برای وضعیت ناموفق HTTP
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "خطا در ارتباط با سرور: " + ex.Message;
                return View(model); // ← مسیر جایگزین برای استثنا
            }
        }

        #endregion


        //public IActionResult Cartable()
        //{
        //    return View("~/Views/Cartable/Index.cshtml", new List<object>());
        //}

        #region Users
        public async Task<IActionResult> Users()
        {
            try
            {
                Console.WriteLine("متد Users فراخوانی شد!");
                Console.WriteLine("در حال ارسال درخواست به http://localhost:5066/api/Auth/GetUsers...");
                var response = await _client.GetAsync("Auth/GetUsers");
                Console.WriteLine($"وضعیت پاسخ: {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<UserViewModel>>();
                    Console.WriteLine($"تعداد کاربران دریافت‌شده: {users?.Count ?? 0}");
                    return View(users);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"خطای API: {error}");
                    ViewBag.ErrorMessage = $"خطا در دریافت کاربران: {error}";
                    return View(new List<UserViewModel>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطا: {ex.Message}");
                Console.WriteLine($"جزئیات خطا: {ex.StackTrace}");
                ViewBag.ErrorMessage = $"خطا در ارتباط با سرور: {ex.Message}";
                return View(new List<UserViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserProfile()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { success = false, message = "توکن یافت نشد." });

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("Auth/GetCurrentUser");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = "دریافت اطلاعات کاربر ناموفق بود." });

            var userInfo = await response.Content.ReadFromJsonAsync<object>();
            return Json(userInfo);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> CreateUser(UserViewModel model)
        //{
        //    try
        //    {
        //        Console.WriteLine("متد CreateUser فراخوانی شد!");
        //        if (!ModelState.IsValid)
        //        {
        //            Console.WriteLine("ModelState نامعتبر است!");
        //            ViewBag.ErrorMessage = "لطفاً همه فیلدها را به درستی پر کنید.";
        //            return View("Users", new List<UserViewModel>());
        //        }

        //        // بررسی تطابق رمز عبور و تأیید رمز عبور
        //        if (model.Password != model.ConfirmPassword)
        //        {
        //            Console.WriteLine("رمز عبور و تأیید رمز عبور یکسان نیستند!");
        //            ViewBag.ErrorMessage = "رمز عبور و تأیید رمز عبور باید یکسان باشند.";
        //            return View("Users", new List<UserViewModel>());
        //        }

        //        Console.WriteLine("در حال ارسال درخواست به http://localhost:5066/api/Auth/CreateUser...");
        //        var response = await _client.PostAsJsonAsync("Auth/CreateUser", model);
        //        Console.WriteLine($"وضعیت پاسخ: {response.StatusCode}");
        //        if (response.IsSuccessStatusCode)
        //        {
        //            Console.WriteLine("کاربر با موفقیت اضافه شد!");
        //            return RedirectToAction("Users");
        //        }
        //        else
        //        {
        //            var error = await response.Content.ReadAsStringAsync();
        //            Console.WriteLine($"خطای API: {error}");
        //            ViewBag.ErrorMessage = $"خطا در افزودن کاربر: {error}";
        //            return View("Users", new List<UserViewModel>());
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"خطا: {ex.Message}");
        //        Console.WriteLine($"جزئیات خطا: {ex.StackTrace}");
        //        ViewBag.ErrorMessage = $"خطا در ارتباط با سرور: {ex.Message}";
        //        return View("Users", new List<UserViewModel>());
        //    }
        //}

        public async Task<IActionResult> EditProfile()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.ErrorMessage = "لطفاً ابتدا وارد سیستم شوید.";
                return RedirectToAction("LoginPage");
            }

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("Auth/GetCurrentUser");
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.ErrorMessage = "دریافت اطلاعات کاربر ناموفق بود.";
                return View(new UserInfo());
            }

            var userInfo = await response.Content.ReadFromJsonAsync<UserInfo>();
            return View(userInfo);
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
            return Json(new { success = false, message = errorObj.message ?? $"خطا در تغییر رمز عبور: {error}" });
        }

        #endregion

        public IActionResult Shahkar()
        {
            return View();
        }
        
    }
    public class ChangePasswordViewModel
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
    }

    public class UserProfileViewModel
    {
        public string Name { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }
    }

    public class LoginResponse
    {
        public string Message { get; set; }
        public UserInfo User { get; set; }
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

    public class TokenInfo
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}