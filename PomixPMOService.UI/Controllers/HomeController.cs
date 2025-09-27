using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;
using PomixPMOService.UI.ViewModels;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

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
                    if (loginResponse?.Tokens?.AccessToken == null)
                    {
                        ViewBag.ErrorMessage = "خطا: توکن دریافت نشد.";
                        return View(model);
                    }

                    // ارسال توکن و پیام به View
                    ViewBag.JwtToken = loginResponse.Tokens.AccessToken;
                    ViewBag.SuccessMessage = loginResponse.Message;
                    return View("LoginSuccess", model);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ViewBag.ErrorMessage = "خطا در ورود: " + error;
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "خطا در ارتباط با سرور: " + ex.Message;
                return View(model);
            }
        }


        public IActionResult Cartable()
        {
            return View("~/Views/Cartable/Cartable.cshtml", new List<object>());
        }

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

        public IActionResult EditProfile()
        {
            return View();
        }

        public IActionResult Shahkar()
        {
            return View();
        }
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