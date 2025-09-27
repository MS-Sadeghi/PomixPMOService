using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;
using PomixPMOService.UI.ViewModels;
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

                    ViewBag.JwtToken = loginResponse.Tokens.AccessToken;
                    ViewBag.SuccessMessage = loginResponse.Message;

                    return View(model);
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
            return View("~/Views/Cartable/Index.cshtml", new List<object>());
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            try
            {
                var jwtToken = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(jwtToken))
                {
                    return RedirectToAction("LoginPage");
                }

                _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

                var response = await _client.GetAsync("api/Auth");
                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<UserViewModel>>();
                    return View(users);
                }
                else
                {
                    ViewBag.ErrorMessage = "خطا در دریافت لیست کاربران: " + await response.Content.ReadAsStringAsync();
                    return View(new List<UserViewModel>());
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "خطا در ارتباط با سرور: " + ex.Message;
                return View(new List<UserViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "لطفا همه فیلدها را به درستی وارد کنید.";
                return View("Users", new List<UserViewModel>());
            }

            try
            {
                var jwtToken = HttpContext.Session.GetString("JwtToken");
                if (string.IsNullOrEmpty(jwtToken))
                {
                    return RedirectToAction("LoginPage");
                }

                _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

                var response = await _client.PostAsJsonAsync("api/Auth/register", model);
                if (response.IsSuccessStatusCode)
                {
                    ViewBag.SuccessMessage = "کاربر با موفقیت ثبت شد.";
                    var usersResponse = await _client.GetAsync("api/Auth");
                    var users = usersResponse.IsSuccessStatusCode
                        ? await usersResponse.Content.ReadFromJsonAsync<List<UserViewModel>>()
                        : new List<UserViewModel>();
                    return View("Users", users);
                }
                else
                {
                    ViewBag.ErrorMessage = "خطا در ثبت کاربر: " + await response.Content.ReadAsStringAsync();
                    return View("Users", new List<UserViewModel>());
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "خطا در ارتباط با سرور: " + ex.Message;
                return View("Users", new List<UserViewModel>());
            }
        }

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