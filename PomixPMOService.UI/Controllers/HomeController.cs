using Microsoft.AspNetCore.Mvc;
using PomixPMOService.UI.ViewModels;

namespace PomixPMOService.UI.Controllers
{
    public class HomeController : Controller
    {
        private readonly HttpClient _client;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("PomixApi");
        }

        [HttpGet]
        public IActionResult LoginPage()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> LoginPage(LoginViewModel model)
        {
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
                    // اگر توکن یا info دیگه میخوای میتونی اینجا ذخیره کنی
                    return RedirectToAction("Index", "Cartable");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ViewBag.ErrorMessage = "خطا در ورود: " + error;
                    return View(model);
                }
            }
            catch
            {
                ViewBag.ErrorMessage = "خطا در ارتباط با سرور.";
                return View(model);
            }
        }

        public IActionResult Cartable()
        {
            return View("~/Views/Cartable/Cartable.cshtml", new List<object>());
        }

        public IActionResult Users()
        {
            return View();
        }

        public IActionResult EditProfile()
        {
            return View();
        }
    }
}
